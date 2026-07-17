using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using RSS_Reader.Models;
using RSS_Reader.Services;
using RSS_Reader.Storage;

namespace RSS_Reader.Endpoints;

public static class ChatEndpoints
{
    public static void MapChatEndpoints(this WebApplication app)
    {
        // Chat now works for signed-out visitors too, but only ever in-memory for them - a
        // guest's conversation history is never written to the database at all. It exists
        // only as long as the browser tab holds onto it (the frontend keeps it in a plain JS
        // variable, not localStorage), so a refresh naturally wipes it. Signed-in users still
        // get real, persisted, per-account history as before. No .RequireAuthorization() on
        // this group anymore - each route below branches on whether a user id is present.
        var group = app.MapGroup("/chat");

        group.MapGet("/messages", (HttpContext http, IChatStore chatStore) =>
        {
            var userId = AuthEndpoints.GetUserId(http);
            // No persisted history exists for a guest - nothing to return.
            if (!userId.HasValue) return Results.Ok(new List<ChatMessage>());
            return Results.Ok(chatStore.LoadMessages(userId.Value));
        });

        group.MapPost("", async (HttpContext http, ChatRequest req, IChatStore chatStore, IArticleStore articleStore, IFeedStore feedStore, HttpClient httpClient, IConfiguration configuration, AiRateTracker tracker) =>
        {
            var userId = AuthEndpoints.GetUserId(http);
            var userIdStr = userId?.ToString();

            var key = userIdStr ?? http.Connection.RemoteIpAddress?.ToString() ?? "anon";
            var isAuth = userId.HasValue;
            var (remaining, limit) = tracker.RecordAndGetStatus(key, isAuth);
            http.Response.Headers["X-RateLimit-Remaining"] = remaining.ToString();
            http.Response.Headers["X-RateLimit-Limit"] = limit.ToString();

            if (remaining <= 0)
                return Results.Problem("AI rate limit reached. Try again shortly.", statusCode: 429);
            var userFeeds = feedStore.Load(userIdStr);
            var feedIds = new HashSet<Guid>(userFeeds.Select(f => f.Id));

            var apiKey = configuration["DEEPSEEK_API_KEY"];
            if (string.IsNullOrWhiteSpace(apiKey))
                return Results.Problem("DEEPSEEK_API_KEY not configured", statusCode: 400);

            if (string.IsNullOrWhiteSpace(req.Message))
                return Results.BadRequest("Message is required");

            if (userId.HasValue)
            {
                chatStore.AddMessage(userId.Value, new ChatMessage
                {
                    Role = "user",
                    Content = req.Message,
                    CreatedAt = DateTime.UtcNow
                });
            }

            var messages = new List<object>
            {
                new { role = "system", content = "You are a helpful news assistant for the user's personal RSS reader. You can summarize articles, answer questions, and help the user understand their feed content. Keep responses concise and informative. You may use basic markdown to structure replies clearly - **bold** for emphasis, and bullet or numbered lists when listing multiple items - since the app renders this formatting rather than showing raw symbols.\n\nIMPORTANT: Base your answers ONLY on the article list below - do not use outside/general knowledge and do not invent, guess, or embellish any story, statistic, or detail that isn't actually in the list. If the user asks about a topic (e.g. sports, a specific company) and none of the articles below cover it, say plainly that there's nothing on that topic in their current feeds instead of making something up.\n\nMost recent articles across your subscribed feeds:\n" + GetArticleContext(articleStore, feedIds) }
            };

            if (userId.HasValue)
            {
                // Signed in - real, persisted, per-account history from the database.
                var history = chatStore.LoadMessages(userId.Value);
                foreach (var msg in history.TakeLast(20))
                {
                    messages.Add(new { role = msg.Role, content = msg.Content });
                }
            }
            else
            {
                // Guest - nothing persisted server-side, so the ongoing conversation only
                // exists because the frontend sends it back with every request. Capped the
                // same way (last 20 turns) to keep the payload/prompt size sane.
                if (req.History != null)
                {
                    foreach (var turn in req.History.TakeLast(20))
                    {
                        if (string.IsNullOrWhiteSpace(turn.Role) || string.IsNullOrWhiteSpace(turn.Content)) continue;
                        messages.Add(new { role = turn.Role, content = turn.Content });
                    }
                }
                messages.Add(new { role = "user", content = req.Message });
            }

            var requestBody = new
            {
                model = "deepseek-chat",
                max_tokens = 1000,
                messages
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.deepseek.com/v1/chat/completions");
            request.Headers.Add("Authorization", $"Bearer {apiKey}");
            request.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

            using var response = await httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                return Results.Problem($"DeepSeek API error: {errorBody}", statusCode: 502);
            }

            var rawResponse = await response.Content.ReadAsStringAsync();
            string reply;
            try
            {
                using var responseDoc = JsonDocument.Parse(rawResponse);
                reply = responseDoc.RootElement
                    .GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString() ?? "";
            }
            catch (JsonException)
            {
                return Results.Problem("The AI response could not be read - please try again.", statusCode: 502);
            }

            if (userId.HasValue)
            {
                chatStore.AddMessage(userId.Value, new ChatMessage
                {
                    Role = "assistant",
                    Content = reply.Trim(),
                    CreatedAt = DateTime.UtcNow
                });
            }

            return Results.Ok(new { reply = reply.Trim() });
        }).RequireRateLimiting("ai");

        group.MapDelete("/messages", (HttpContext http, IChatStore chatStore) =>
        {
            var userId = AuthEndpoints.GetUserId(http);
            if (userId.HasValue) chatStore.Clear(userId.Value);
            return Results.NoContent();
        });
    }

    private static string GetArticleContext(IArticleStore articleStore, HashSet<Guid> feedIds)
    {
        var articles = articleStore.Load()
            .Where(a => feedIds.Count == 0 || feedIds.Contains(a.FeedId))
            .OrderByDescending(a => a.PublishDate)
            .Take(60)
            .ToList();

        if (articles.Count == 0) return "No articles available yet.";

        return string.Join("\n", articles.Select(a =>
        {
            var summary = a.Summary.Length > 220 ? a.Summary[..220] + "…" : a.Summary;
            return $"- [{a.FeedTitle} | {a.PublishDate:yyyy-MM-dd}] {a.Title}: {summary}";
        }));
    }
}

public record ChatRequest(string Message, List<ChatTurn>? History);
public record ChatTurn(string Role, string Content);