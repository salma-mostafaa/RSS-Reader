using System.Linq;
using RSS_Reader.Storage;

namespace RSS_Reader.Endpoints;

public static class SavedEndpoints
{
    public static void MapSavedEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/saved").RequireAuthorization();

        group.MapGet("/", (HttpContext http, ISavedStore store) =>
        {
            var userId = AuthEndpoints.GetUserId(http)!.Value;
            return Results.Ok(store.LoadAll(userId));
        });

        group.MapPost("/{articleId}", (HttpContext http, Guid articleId, IArticleStore articleStore, ISavedStore store) =>
        {
            var userId = AuthEndpoints.GetUserId(http)!.Value;
            var article = articleStore.Load().FirstOrDefault(a => a.Id == articleId);
            if (article == null)
                return Results.NotFound("Article not found - it may have aged out of retention");

            var entry = store.AddIfNotPresent(userId, article);
            if (entry == null)
                return Results.BadRequest("Already saved");

            return Results.Ok(entry);
        });

        group.MapDelete("/{savedItemId}", (HttpContext http, Guid savedItemId, ISavedStore store) =>
        {
            var userId = AuthEndpoints.GetUserId(http)!.Value;
            return store.Remove(userId, savedItemId) ? Results.NoContent() : Results.NotFound();
        });
    }
}