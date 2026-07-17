using RSS_Reader.Models;
using RSS_Reader.Services;
using RSS_Reader.Storage;

namespace RSS_Reader.Endpoints;

public static class FeedEndpoints
{
    public static void MapFeedEndpoints(this WebApplication app)
    {
        app.MapGet("/feeds", (IFeedStore feedStore, HttpContext http) =>
        {
            var userId = AuthEndpoints.GetUserId(http)?.ToString();
            return feedStore.Load(userId);
        });

        app.MapPost("/feeds", async (Feed feed, IFeedStore feedStore, IArticleStore articleStore, FeedService feedService, HttpClient httpClient, HttpContext http) =>
        {
            var userId = AuthEndpoints.GetUserId(http);
            var allFeeds = feedStore.Load(userId?.ToString());

            feed.Id = Guid.NewGuid();
            feed.Url = feed.Url?.Trim() ?? "";
            Console.WriteLine($"[FEED] POST /feeds url={feed.Url} user={userId}");
            if (userId.HasValue) feed.UserId = userId.Value.ToString();

            if (!Uri.TryCreate(feed.Url, UriKind.Absolute, out _))
            {
                Console.WriteLine($"[FEED] Invalid URL: {feed.Url}");
                return Results.BadRequest("Invalid URL");
            }

            if (allFeeds.Any(f => f.Url == feed.Url))
            {
                Console.WriteLine($"[FEED] Duplicate URL: {feed.Url}");
                return Results.BadRequest("Feed already exists");
            }

            var result = await feedService.AddFeedAsync(feed, httpClient);
            if (!result.Success)
            {
                Console.WriteLine($"[FEED] AddFeedAsync failed: {result.Error}");
                return Results.BadRequest(result.Error);
            }

            if (userId.HasValue)
            {
                allFeeds.Add(feed);
                feedStore.SaveUserFeeds(allFeeds, userId.Value.ToString());
            }

            articleStore.MergeAndPersist(feed.Id, result.InitialArticles);
            return Results.Ok(feed);
        });

        app.MapDelete("/feeds/{id}", (Guid id, IFeedStore feedStore, IArticleStore articleStore, HttpContext http) =>
        {
            var userId = AuthEndpoints.GetUserId(http);
            if (userId == null) return Results.Unauthorized();

            var allFeeds = feedStore.Load(userId.Value.ToString());
            var feed = allFeeds.FirstOrDefault(f => f.Id == id);

            if (feed == null) return Results.NotFound();

            if (!string.IsNullOrEmpty(feed.UserId) && feed.UserId != userId.Value.ToString())
                return Results.Forbid();

            allFeeds.Remove(feed);
            feedStore.SaveUserFeeds(allFeeds, userId.Value.ToString());
            articleStore.RemoveForFeed(id);
            return Results.NoContent();
        });

        app.MapPost("/feeds/{id}/refresh", async (Guid id, IFeedStore feedStore, IArticleStore articleStore, FeedService feedService, HttpClient httpClient, HttpContext http) =>
        {
            var userId = AuthEndpoints.GetUserId(http)?.ToString();
            var allFeeds = feedStore.Load(userId);
            var feed = allFeeds.FirstOrDefault(f => f.Id == id);

            if (feed == null) return Results.NotFound();

            var freshArticles = await feedService.FetchArticlesForFeedAsync(feed, httpClient);
            var merged = articleStore.MergeAndPersist(feed.Id, freshArticles);
            return Results.Ok(merged.OrderByDescending(a => a.PublishDate).ToList());
        });
    }
}
