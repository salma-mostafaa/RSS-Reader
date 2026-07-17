using System.Security.Claims;
using RSS_Reader.Models;
using RSS_Reader.Services;
using RSS_Reader.Storage;

namespace RSS_Reader.Endpoints;

public static class SummaryEndpoints
{
    public static void MapSummaryEndpoints(this WebApplication app)
    {
        app.MapGet("/summary", (ISummaryStore summaryStore, IFeedStore feedStore, HttpContext http) =>
        {
            var userId = AuthEndpoints.GetUserId(http)?.ToString();
            var summary = summaryStore.Load(userId);
            return Results.Ok(summary ?? new AiSummary { Text = "No summary generated yet.", TextEn = "No summary generated yet.", TextAr = "لا يوجد ملخص بعد.", GeneratedAt = DateTime.MinValue, ArticleCount = 0 });
        });

        app.MapPost("/summary/generate", async (AiSummaryService aiSummaryService, IFeedStore feedStore, HttpClient httpClient, HttpContext http, AiRateTracker tracker) =>
        {
            var key = http.User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier)
                      ?? http.Connection.RemoteIpAddress?.ToString() ?? "anon";
            var isAuth = http.User.Identity?.IsAuthenticated == true;
            var (remaining, limit) = tracker.RecordAndGetStatus(key, isAuth);
            http.Response.Headers["X-RateLimit-Remaining"] = remaining.ToString();
            http.Response.Headers["X-RateLimit-Limit"] = limit.ToString();

            if (remaining <= 0)
                return Results.Problem("AI rate limit reached. Try again shortly.", statusCode: 429);

            try
            {
                var userId = AuthEndpoints.GetUserId(http)?.ToString();
                var feeds = feedStore.Load(userId);
                var feedIds = feeds.Count > 0 ? new HashSet<Guid>(feeds.Select(f => f.Id)) : null;

                var summary = await aiSummaryService.GenerateAsync(httpClient, feedIds, userId);
                return Results.Ok(summary);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Problem(ex.Message, statusCode: 400);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Could not generate summary: {ex.Message}", statusCode: 502);
            }
        }).RequireRateLimiting("ai");
    }
}
