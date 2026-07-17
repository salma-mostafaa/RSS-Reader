using RSS_Reader.Services;
using RSS_Reader.Storage;

namespace RSS_Reader.Endpoints;

public static class ArticleEndpoints
{
    public static void MapArticleEndpoints(this WebApplication app)
    {
        app.MapGet("/articles", (IArticleStore articleStore, IFeedStore feedStore, HttpContext http, string? extraFeedIds) =>
        {
            var userId = AuthEndpoints.GetUserId(http)?.ToString();
            var allFeeds = feedStore.Load(userId);
            var feedIds = new HashSet<Guid>(allFeeds.Select(f => f.Id));

            if (!string.IsNullOrEmpty(extraFeedIds))
            {
                foreach (var id in extraFeedIds.Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (Guid.TryParse(id.Trim(), out var gid))
                        feedIds.Add(gid);
                }
            }

            var articles = articleStore.Load()
                .Where(a => feedIds.Contains(a.FeedId))
                .OrderByDescending(a => a.PublishDate)
                .ToList();

            return Results.Ok(articles);
        });

        app.MapPost("/articles/refresh-all", async (IFeedStore feedStore, IArticleStore articleStore, FeedService feedService, HttpClient httpClient, HttpContext http) =>
        {
            var userId = AuthEndpoints.GetUserId(http)?.ToString();
            var feeds = feedStore.Load(userId);

            foreach (var feed in feeds)
            {
                try
                {
                    var freshArticles = await feedService.FetchArticlesForFeedAsync(feed, httpClient);
                    articleStore.MergeAndPersist(feed.Id, freshArticles);
                }
                catch { }
            }

            var feedIds = new HashSet<Guid>(feeds.Select(f => f.Id));
            var articles = articleStore.Load()
                .Where(a => feedIds.Contains(a.FeedId))
                .OrderByDescending(a => a.PublishDate)
                .ToList();

            return Results.Ok(articles);
        });
    }
}
