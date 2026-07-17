using RSS_Reader.Models;

namespace RSS_Reader.Storage;

public interface IPlaylistStore
{
    List<Playlist> LoadPlaylists(Guid userId);
    Playlist? GetPlaylist(Guid userId, Guid id);
    Playlist CreatePlaylist(Guid userId, string name);
    Playlist? RenamePlaylist(Guid userId, Guid id, string name);
    bool DeletePlaylist(Guid userId, Guid id);

    List<PlaylistItem> LoadItems(Guid userId, Guid playlistId);
    PlaylistItem? AddIfNotPresent(Guid userId, Guid playlistId, Article article);
    bool Remove(Guid userId, Guid playlistItemId);

    // Used only by the public RSS export route (/playlist/{id}/feed.xml), which external
    // RSS readers hit with no login cookie at all - ownership there is "knowing the exact
    // playlist ID", same trust model as any other unguessable share link.
    Playlist? GetPlaylistUnscoped(Guid id);
    List<PlaylistItem> LoadItemsUnscoped(Guid playlistId);
}