using System.ServiceModel.Syndication;
using System.Text;
using System.Xml;
using RSS_Reader.Models;
using RSS_Reader.Storage;

namespace RSS_Reader.Endpoints;

public static class PlaylistEndpoints
{
    public static void MapPlaylistEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("").RequireAuthorization();

        group.MapGet("/playlists", (HttpContext http, IPlaylistStore store) =>
        {
            var userId = AuthEndpoints.GetUserId(http)!.Value;
            return Results.Ok(store.LoadPlaylists(userId));
        });

        group.MapPost("/playlists", (HttpContext http, IPlaylistStore store, CreatePlaylistRequest req) =>
        {
            if (string.IsNullOrWhiteSpace(req.Name))
                return Results.BadRequest("Playlist name is required");

            var userId = AuthEndpoints.GetUserId(http)!.Value;
            var playlist = store.CreatePlaylist(userId, req.Name);
            return Results.Ok(playlist);
        });

        group.MapPut("/playlists/{id}", (HttpContext http, Guid id, IPlaylistStore store, RenamePlaylistRequest req) =>
        {
            if (string.IsNullOrWhiteSpace(req.Name))
                return Results.BadRequest("Playlist name is required");

            var userId = AuthEndpoints.GetUserId(http)!.Value;
            var playlist = store.RenamePlaylist(userId, id, req.Name);
            if (playlist == null) return Results.NotFound();
            return Results.Ok(playlist);
        });

        group.MapDelete("/playlists/{id}", (HttpContext http, Guid id, IPlaylistStore store) =>
        {
            var userId = AuthEndpoints.GetUserId(http)!.Value;
            return store.DeletePlaylist(userId, id) ? Results.NoContent() : Results.NotFound();
        });

        group.MapGet("/playlist/{playlistId}/items", (HttpContext http, Guid playlistId, IPlaylistStore store) =>
        {
            var userId = AuthEndpoints.GetUserId(http)!.Value;
            return Results.Ok(store.LoadItems(userId, playlistId));
        });

        group.MapPost("/playlist/{playlistId}/{articleId}", (HttpContext http, Guid playlistId, Guid articleId, IArticleStore articleStore, IPlaylistStore store) =>
        {
            var userId = AuthEndpoints.GetUserId(http)!.Value;
            var article = articleStore.Load().FirstOrDefault(a => a.Id == articleId);
            if (article == null)
                return Results.NotFound("Article not found - it may have aged out of retention");

            var entry = store.AddIfNotPresent(userId, playlistId, article);
            if (entry == null)
                return Results.BadRequest("Already in this playlist");

            return Results.Ok(entry);
        });

        group.MapDelete("/playlist/{playlistItemId}", (HttpContext http, Guid playlistItemId, IPlaylistStore store) =>
        {
            var userId = AuthEndpoints.GetUserId(http)!.Value;
            return store.Remove(userId, playlistItemId) ? Results.NoContent() : Results.NotFound();
        });

        // Each playlist's exported RSS feed is meant to be pulled by *external* RSS readers,
        // which obviously don't have a login cookie - so this stays public. Anyone with the
        // exact playlist ID (a random GUID, not guessable) can read it, same as any "share
        // link" pattern - it just isn't gated by the owner's session the way the app UI is.
        app.MapGet("/playlist/{playlistId}/feed.xml", (Guid playlistId, IPlaylistStore store) =>
        {
            var playlist = store.GetPlaylistUnscoped(playlistId);
            if (playlist == null) return Results.NotFound();

            var items = store.LoadItemsUnscoped(playlistId);

            var syndicationItems = items.Select(p => new SyndicationItem(
                p.Article.Title,
                p.Article.Summary,
                new Uri(string.IsNullOrWhiteSpace(p.Article.Link) ? "https://example.com" : p.Article.Link))
            {
                Id = p.Article.Id.ToString(),
                PublishDate = new DateTimeOffset(DateTime.SpecifyKind(p.Article.PublishDate, DateTimeKind.Utc))
            });

            var feed = new SyndicationFeed(
                playlist.Name,
                "Articles curated from my personal RSS reader",
                new Uri("https://example.com/playlist"),
                syndicationItems);

            return SyndicateFeed(feed);
        });
    }

    private static IResult SyndicateFeed(SyndicationFeed feed)
    {
        using var stream = new MemoryStream();
        using (var xmlWriter = XmlWriter.Create(stream, new XmlWriterSettings { Encoding = Encoding.UTF8, Indent = true }))
        {
            new Rss20FeedFormatter(feed).WriteTo(xmlWriter);
        }
        return Results.Text(Encoding.UTF8.GetString(stream.ToArray()), "application/rss+xml");
    }
}

public record CreatePlaylistRequest(string Name);
public record RenamePlaylistRequest(string Name);