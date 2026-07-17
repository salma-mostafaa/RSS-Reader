using System.Text.Json;
using Microsoft.Data.Sqlite;
using RSS_Reader.Models;

namespace RSS_Reader;

public class Database
{
    private readonly string _connectionString;

    public Database(string connectionString = "Data Source=rss.db")
    {
        _connectionString = connectionString;
    }

    public SqliteConnection Open()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        return conn;
    }

    public void Initialize()
    {
        using var conn = Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS Playlists (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL,
                CreatedAt TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS PlaylistItems (
                Id TEXT PRIMARY KEY,
                PlaylistId TEXT NOT NULL,
                ArticleId TEXT NOT NULL,
                ArticleJson TEXT NOT NULL,
                AddedAt TEXT NOT NULL,
                FOREIGN KEY (PlaylistId) REFERENCES Playlists(Id)
            );

            CREATE TABLE IF NOT EXISTS ChatMessages (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Role TEXT NOT NULL,
                Content TEXT NOT NULL,
                CreatedAt TEXT NOT NULL
            );";
        cmd.ExecuteNonQuery();

        MigratePlaylistFromJson(conn);
    }

    private static void MigratePlaylistFromJson(SqliteConnection conn)
    {
        const string jsonPath = "playlist.json";
        if (!File.Exists(jsonPath)) return;

        using var checkCmd = conn.CreateCommand();
        checkCmd.CommandText = "SELECT COUNT(*) FROM Playlists";
        var count = (long)checkCmd.ExecuteScalar()!;
        if (count > 0) return; // already migrated

        var json = File.ReadAllText(jsonPath);
        var items = JsonSerializer.Deserialize<List<PlaylistItem>>(json);
        if (items == null || items.Count == 0)
        {
            File.Delete(jsonPath);
            return;
        }

        var playlistId = Guid.NewGuid().ToString();
        var playlistName = "My Playlist";

        using var insertPlaylist = conn.CreateCommand();
        insertPlaylist.CommandText = "INSERT INTO Playlists (Id, Name, CreatedAt) VALUES (@id, @name, @created)";
        insertPlaylist.Parameters.AddWithValue("@id", playlistId);
        insertPlaylist.Parameters.AddWithValue("@name", playlistName);
        insertPlaylist.Parameters.AddWithValue("@created", DateTime.UtcNow.ToString("O"));
        insertPlaylist.ExecuteNonQuery();

        var deduped = items
            .GroupBy(p => p.Article.Id)
            .Select(g => g.OrderBy(p => p.AddedAt).First());

        foreach (var item in deduped)
        {
            using var insertItem = conn.CreateCommand();
            insertItem.CommandText = "INSERT INTO PlaylistItems (Id, PlaylistId, ArticleId, ArticleJson, AddedAt) VALUES (@id, @pid, @aid, @json, @added)";
            insertItem.Parameters.AddWithValue("@id", Guid.NewGuid().ToString());
            insertItem.Parameters.AddWithValue("@pid", playlistId);
            insertItem.Parameters.AddWithValue("@aid", item.Article.Id.ToString());
            insertItem.Parameters.AddWithValue("@json", JsonSerializer.Serialize(item.Article));
            insertItem.Parameters.AddWithValue("@added", item.AddedAt.ToString("O"));
            insertItem.ExecuteNonQuery();
        }

        File.Delete(jsonPath);
    }
}
