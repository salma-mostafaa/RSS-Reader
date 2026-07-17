using System.Text.Json;
using Microsoft.Data.Sqlite;
using RSS_Reader.Models;

namespace RSS_Reader.Storage;

public class PlaylistStore : IPlaylistStore
{
    private readonly Database _db;
    private readonly object _lock = new();

    public PlaylistStore(Database db)
    {
        _db = db;
        EnsureUserIdColumn();
    }

    // Playlists/PlaylistItems tables already existed before accounts did, created centrally
    // elsewhere (not by this store). Rather than editing that central setup, this adds the
    // new UserId column on startup if it isn't there yet - safe to run every time, since it
    // checks first instead of blindly re-running ALTER TABLE.
    private void EnsureUserIdColumn()
    {
        using var conn = _db.Open();
        using var checkCmd = conn.CreateCommand();
        checkCmd.CommandText = "PRAGMA table_info(Playlists)";
        using var reader = checkCmd.ExecuteReader();
        var hasUserId = false;
        while (reader.Read())
        {
            if (string.Equals(reader.GetString(1), "UserId", StringComparison.OrdinalIgnoreCase)) { hasUserId = true; break; }
        }
        reader.Close();
        if (!hasUserId)
        {
            using var alterCmd = conn.CreateCommand();
            alterCmd.CommandText = "ALTER TABLE Playlists ADD COLUMN UserId TEXT NOT NULL DEFAULT ''";
            alterCmd.ExecuteNonQuery();
        }
    }

    public List<Playlist> LoadPlaylists(Guid userId)
    {
        var playlists = new List<Playlist>();
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, Name, CreatedAt FROM Playlists WHERE UserId = @uid ORDER BY CreatedAt";
        cmd.Parameters.AddWithValue("@uid", userId.ToString());
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            playlists.Add(new Playlist
            {
                Id = Guid.Parse(reader.GetString(0)),
                UserId = userId,
                Name = reader.GetString(1),
                CreatedAt = DateTime.Parse(reader.GetString(2))
            });
        }
        return playlists;
    }

    public Playlist? GetPlaylist(Guid userId, Guid id)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, Name, CreatedAt FROM Playlists WHERE Id = @id AND UserId = @uid";
        cmd.Parameters.AddWithValue("@id", id.ToString());
        cmd.Parameters.AddWithValue("@uid", userId.ToString());
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;
        return new Playlist
        {
            Id = Guid.Parse(reader.GetString(0)),
            UserId = userId,
            Name = reader.GetString(1),
            CreatedAt = DateTime.Parse(reader.GetString(2))
        };
    }

    public Playlist CreatePlaylist(Guid userId, string name)
    {
        var playlist = new Playlist { Id = Guid.NewGuid(), UserId = userId, Name = name, CreatedAt = DateTime.UtcNow };
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO Playlists (Id, UserId, Name, CreatedAt) VALUES (@id, @uid, @name, @created)";
        cmd.Parameters.AddWithValue("@id", playlist.Id.ToString());
        cmd.Parameters.AddWithValue("@uid", userId.ToString());
        cmd.Parameters.AddWithValue("@name", playlist.Name);
        cmd.Parameters.AddWithValue("@created", playlist.CreatedAt.ToString("O"));
        cmd.ExecuteNonQuery();
        return playlist;
    }

    public Playlist? RenamePlaylist(Guid userId, Guid id, string name)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE Playlists SET Name = @name WHERE Id = @id AND UserId = @uid";
        cmd.Parameters.AddWithValue("@name", name);
        cmd.Parameters.AddWithValue("@id", id.ToString());
        cmd.Parameters.AddWithValue("@uid", userId.ToString());
        if (cmd.ExecuteNonQuery() == 0) return null;
        return new Playlist { Id = id, UserId = userId, Name = name };
    }

    public bool DeletePlaylist(Guid userId, Guid id)
    {
        using var conn = _db.Open();
        using var tx = conn.BeginTransaction();
        try
        {
            using var delItems = conn.CreateCommand();
            delItems.CommandText = @"
                DELETE FROM PlaylistItems WHERE PlaylistId = @pid
                AND PlaylistId IN (SELECT Id FROM Playlists WHERE UserId = @uid)";
            delItems.Parameters.AddWithValue("@pid", id.ToString());
            delItems.Parameters.AddWithValue("@uid", userId.ToString());
            delItems.ExecuteNonQuery();

            using var delPlaylist = conn.CreateCommand();
            delPlaylist.CommandText = "DELETE FROM Playlists WHERE Id = @id AND UserId = @uid";
            delPlaylist.Parameters.AddWithValue("@id", id.ToString());
            delPlaylist.Parameters.AddWithValue("@uid", userId.ToString());
            var affected = delPlaylist.ExecuteNonQuery();

            tx.Commit();
            return affected > 0;
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public List<PlaylistItem> LoadItems(Guid userId, Guid playlistId)
    {
        var items = new List<PlaylistItem>();
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT pi.Id, pi.PlaylistId, pi.ArticleId, pi.ArticleJson, pi.AddedAt
            FROM PlaylistItems pi
            JOIN Playlists p ON p.Id = pi.PlaylistId
            WHERE pi.PlaylistId = @pid AND p.UserId = @uid
            ORDER BY pi.AddedAt DESC";
        cmd.Parameters.AddWithValue("@pid", playlistId.ToString());
        cmd.Parameters.AddWithValue("@uid", userId.ToString());
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var item = new PlaylistItem
            {
                Id = Guid.Parse(reader.GetString(0)),
                PlaylistId = Guid.Parse(reader.GetString(1)),
                ArticleId = Guid.Parse(reader.GetString(2)),
                AddedAt = DateTime.Parse(reader.GetString(4))
            };
            item.ArticleJson = reader.GetString(3);
            items.Add(item);
        }
        return items;
    }

    public PlaylistItem? AddIfNotPresent(Guid userId, Guid playlistId, Article article)
    {
        lock (_lock)
        {
            using var conn = _db.Open();

            using var ownerCmd = conn.CreateCommand();
            ownerCmd.CommandText = "SELECT COUNT(*) FROM Playlists WHERE Id = @pid AND UserId = @uid";
            ownerCmd.Parameters.AddWithValue("@pid", playlistId.ToString());
            ownerCmd.Parameters.AddWithValue("@uid", userId.ToString());
            if ((long)ownerCmd.ExecuteScalar()! == 0) return null; // not your playlist

            using var checkCmd = conn.CreateCommand();
            checkCmd.CommandText = "SELECT COUNT(*) FROM PlaylistItems WHERE PlaylistId = @pid AND ArticleId = @aid";
            checkCmd.Parameters.AddWithValue("@pid", playlistId.ToString());
            checkCmd.Parameters.AddWithValue("@aid", article.Id.ToString());
            if ((long)checkCmd.ExecuteScalar()! > 0) return null;

            var item = new PlaylistItem
            {
                Id = Guid.NewGuid(),
                PlaylistId = playlistId,
                ArticleId = article.Id,
                Article = article,
                AddedAt = DateTime.UtcNow
            };

            using var insertCmd = conn.CreateCommand();
            insertCmd.CommandText = "INSERT INTO PlaylistItems (Id, PlaylistId, ArticleId, ArticleJson, AddedAt) VALUES (@id, @pid, @aid, @json, @added)";
            insertCmd.Parameters.AddWithValue("@id", item.Id.ToString());
            insertCmd.Parameters.AddWithValue("@pid", playlistId.ToString());
            insertCmd.Parameters.AddWithValue("@aid", article.Id.ToString());
            insertCmd.Parameters.AddWithValue("@json", JsonSerializer.Serialize(article));
            insertCmd.Parameters.AddWithValue("@added", item.AddedAt.ToString("O"));
            insertCmd.ExecuteNonQuery();

            return item;
        }
    }

    public bool Remove(Guid userId, Guid playlistItemId)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            DELETE FROM PlaylistItems WHERE Id = @id
            AND PlaylistId IN (SELECT Id FROM Playlists WHERE UserId = @uid)";
        cmd.Parameters.AddWithValue("@id", playlistItemId.ToString());
        cmd.Parameters.AddWithValue("@uid", userId.ToString());
        return cmd.ExecuteNonQuery() > 0;
    }

    public Playlist? GetPlaylistUnscoped(Guid id)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, UserId, Name, CreatedAt FROM Playlists WHERE Id = @id";
        cmd.Parameters.AddWithValue("@id", id.ToString());
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;
        return new Playlist
        {
            Id = Guid.Parse(reader.GetString(0)),
            UserId = Guid.TryParse(reader.GetString(1), out var uid) ? uid : Guid.Empty,
            Name = reader.GetString(2),
            CreatedAt = DateTime.Parse(reader.GetString(3))
        };
    }

    public List<PlaylistItem> LoadItemsUnscoped(Guid playlistId)
    {
        var items = new List<PlaylistItem>();
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, PlaylistId, ArticleId, ArticleJson, AddedAt FROM PlaylistItems WHERE PlaylistId = @pid ORDER BY AddedAt DESC";
        cmd.Parameters.AddWithValue("@pid", playlistId.ToString());
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var item = new PlaylistItem
            {
                Id = Guid.Parse(reader.GetString(0)),
                PlaylistId = Guid.Parse(reader.GetString(1)),
                ArticleId = Guid.Parse(reader.GetString(2)),
                AddedAt = DateTime.Parse(reader.GetString(4))
            };
            item.ArticleJson = reader.GetString(3);
            items.Add(item);
        }
        return items;
    }
}