using System.Linq;
using RSS_Reader.Storage;

namespace RSS_Reader.Endpoints;

public static class HistoryEndpoints
{
    public static void MapHistoryEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/history").RequireAuthorization();

        group.MapGet("/items", (HttpContext http, IHistoryStore store) =>
        {
            var userId = AuthEndpoints.GetUserId(http)!.Value;
            return Results.Ok(store.LoadAll(userId));
        });

        // Lightweight - just the ids of articles already viewed, so the frontend can dim
        // "already read" cards without downloading full article snapshots on every page load.
        group.MapGet("/ids", (HttpContext http, IHistoryStore store) =>
        {
            var userId = AuthEndpoints.GetUserId(http)!.Value;
            return Results.Ok(store.LoadViewedArticleIds(userId));
        });

        group.MapPost("/{articleId}", (HttpContext http, Guid articleId, IArticleStore articleStore, IHistoryStore store) =>
        {
            var userId = AuthEndpoints.GetUserId(http)!.Value;
            var article = articleStore.Load().FirstOrDefault(a => a.Id == articleId);
            if (article == null)
                return Results.NotFound("Article not found - it may have aged out of retention");

            var entry = store.LogView(userId, article);
            return Results.Ok(entry);
        });

        group.MapDelete("/", (HttpContext http, IHistoryStore store) =>
        {
            var userId = AuthEndpoints.GetUserId(http)!.Value;
            store.Clear(userId);
            return Results.NoContent();
        });
    }
}