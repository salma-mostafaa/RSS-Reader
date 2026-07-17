using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using RSS_Reader.Models;
using RSS_Reader.Services;
using RSS_Reader.Storage;

namespace RSS_Reader.Endpoints;

public static class ArticleSummaryEndpoints
{
    public static void MapArticleSummaryEndpoints(this WebApplication app)
    {
        // Returns the cached summary if one exists yet - doesn't generate one. The frontend
        // uses this to decide whether to show a "Summarize" button or the existing result.
        app.MapGet("/articles/{articleId}/summary", (Guid articleId, IArticleSummaryStore summaryStore) =>
        {
            var summary = summaryStore.Get(articleId);
            return summary == null ? Results.NotFound() : Results.Ok(summary);
        });

        app.MapPost("/articles/{articleId}/summarize", async (
            Guid articleId,
            IArticleStore articleStore,
            IArticleSummaryStore summaryStore,
            ISavedStore savedStore,
            IHistoryStore historyStore,
            IPlaylistStore playlistStore,
            IConfiguration configuration,
            HttpClient httpClient,
            HttpContext http,
            AiRateTracker tracker) =>
        {
            var key = http.User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier)
                      ?? http.Connection.RemoteIpAddress?.ToString() ?? "anon";
            var isAuth = http.User.Identity?.IsAuthenticated == true;
            var (remaining, limit) = tracker.RecordAndGetStatus(key, isAuth);
            http.Response.Headers["X-RateLimit-Remaining"] = remaining.ToString();
            http.Response.Headers["X-RateLimit-Limit"] = limit.ToString();

            if (remaining <= 0)
                return Results.Problem("AI rate limit reached. Try again shortly.", statusCode: 429);
            var apiKey = configuration["DEEPSEEK_API_KEY"];
            if (string.IsNullOrWhiteSpace(apiKey))
                return Results.Problem("DEEPSEEK_API_KEY not configured", statusCode: 400);

            var article = articleStore.Load().FirstOrDefault(a => a.Id == articleId);

            // Articles shown on the Saved/History/Playlist pages are stored snapshots that can
            // outlive the main article store's retention window. If the article has aged out of
            // (or was never in) the live store, fall back to the signed-in user's snapshots so
            // "Summarize" keeps working on those pages instead of failing with a 404.
            if (article == null)
            {
                var userId = AuthEndpoints.GetUserId(http);
                if (userId != null)
                {
                    article = savedStore.LoadAll(userId.Value).FirstOrDefault(s => s.ArticleId == articleId)?.Article
                        ?? historyStore.LoadAll(userId.Value).FirstOrDefault(h => h.ArticleId == articleId)?.Article
                        ?? playlistStore.LoadPlaylists(userId.Value)
                            .SelectMany(pl => playlistStore.LoadItems(userId.Value, pl.Id))
                            .FirstOrDefault(i => i.ArticleId == articleId)?.Article;
                }
            }

            if (article == null) return Results.NotFound("Article not found");

            // Already generated - return the cached version instead of paying for another call.
            var existing = summaryStore.Get(articleId);
            if (existing != null) return Results.Ok(existing);

            // Note: this summarizes the article's own title/summary text as pulled from its
            // feed - for podcast/audio entries that only really means the episode description,
            // not the actual audio content. Real audio transcription would be a separate,
            // heavier feature (speech-to-text + summarization), not something this endpoint does.
            var prompt = "Summarize ONLY the article below in 2-3 concise sentences, plain text, no markdown. " +
                "Do not add outside information or speculate beyond what's written. " +
                "Respond with ONLY a valid JSON object (no markdown, no code fences) with exactly two keys: " +
                "\"en\" (the summary in English) and \"ar\" (the same summary translated into natural Arabic).\n\n" +
                $"Title: {article.Title}\n\nContent: {article.Summary}";

            var requestBody = new
            {
                model = "deepseek-chat",
                max_tokens = 400,
                response_format = new { type = "json_object" },
                messages = new[] { new { role = "user", content = prompt } }
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

            using var responseDoc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var choice = responseDoc.RootElement.GetProperty("choices")[0];

            var finishReason = choice.TryGetProperty("finish_reason", out var frEl) ? frEl.GetString() : null;
            if (finishReason == "length")
                return Results.Problem("The AI response was cut off before it finished - please try again.", statusCode: 502);

            var rawContent = choice.GetProperty("message").GetProperty("content").GetString() ?? "";

            string textEn, textAr;
            try
            {
                var cleaned = rawContent.Trim();
                var firstBrace = cleaned.IndexOf('{');
                var lastBrace = cleaned.LastIndexOf('}');
                if (firstBrace >= 0 && lastBrace > firstBrace) cleaned = cleaned[firstBrace..(lastBrace + 1)];
                using var doc = JsonDocument.Parse(cleaned);
                textEn = doc.RootElement.TryGetProperty("en", out var enEl) ? (enEl.GetString() ?? "").Trim() : "";
                textAr = doc.RootElement.TryGetProperty("ar", out var arEl) ? (arEl.GetString() ?? "").Trim() : "";
            }
            catch (JsonException)
            {
                return Results.Problem("The AI response could not be read as valid summary data - please try again.", statusCode: 502);
            }

            // Stored as proper TextEn/TextAr fields so the frontend can pick whichever
            // language matches the current UI without a second call or extra parsing.
            var summary = new ArticleSummary { ArticleId = articleId, TextEn = textEn, TextAr = textAr, GeneratedAt = DateTime.UtcNow };
            summaryStore.Save(summary);

            return Results.Ok(summary);
        }).RequireRateLimiting("ai");
    }
}
