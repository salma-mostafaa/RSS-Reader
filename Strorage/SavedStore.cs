using System.Text.Json;
using Microsoft.Data.Sqlite;
using RSS_Reader.Models;

namespace RSS_Reader.Storage;

// A flat "Save for later" list - deliberately simpler than Playlists (no naming, no multiple
// lists, no per-list feed). One tap to save, one place to find it again.
public class SavedStore : ISavedStore
{
    private readonly Database _db;
    private readonly object _lock = new();

    public SavedStore(Database db)
    {
        _db = db;
        EnsureTable();
        EnsureUserIdColumn();
    }

    private void EnsureTable()
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS SavedArticles (
                Id TEXT PRIMARY KEY,
                UserId TEXT NOT NULL,
                ArticleId TEXT NOT NULL,
                ArticleJson TEXT NOT NULL,
                SavedAt TEXT NOT NULL
            )";
        cmd.ExecuteNonQuery();
    }

    // The SavedArticles table already existed from before user accounts did (created without
    // a UserId column at all). CREATE TABLE IF NOT EXISTS above is a no-op once the table
    // already exists, so this adds the missing column on startup if needed - safe to run
    // every time since it checks first.
    private void EnsureUserIdColumn()
    {
        using var conn = _db.Open();
        using var checkCmd = conn.CreateCommand();
        checkCmd.CommandText = "PRAGMA table_info(SavedArticles)";
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
            alterCmd.CommandText = "ALTER TABLE SavedArticles ADD COLUMN UserId TEXT NOT NULL DEFAULT ''";
            alterCmd.ExecuteNonQuery();
        }
    }

    public List<SavedArticle> LoadAll(Guid userId)
    {
        var items = new List<SavedArticle>();
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, ArticleId, ArticleJson, SavedAt FROM SavedArticles WHERE UserId = @uid ORDER BY SavedAt DESC";
        cmd.Parameters.AddWithValue("@uid", userId.ToString());
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var item = new SavedArticle
            {
                Id = Guid.Parse(reader.GetString(0)),
                ArticleId = Guid.Parse(reader.GetString(1)),
                SavedAt = DateTime.Parse(reader.GetString(3))
            };
            item.ArticleJson = reader.GetString(2);
            items.Add(item);
        }
        return items;
    }

    public SavedArticle? AddIfNotPresent(Guid userId, Article article)
    {
        lock (_lock)
        {
            using var conn = _db.Open();
            using var checkCmd = conn.CreateCommand();
            checkCmd.CommandText = "SELECT COUNT(*) FROM SavedArticles WHERE UserId = @uid AND ArticleId = @aid";
            checkCmd.Parameters.AddWithValue("@uid", userId.ToString());
            checkCmd.Parameters.AddWithValue("@aid", article.Id.ToString());
            if ((long)checkCmd.ExecuteScalar()! > 0) return null;

            var item = new SavedArticle
            {
                Id = Guid.NewGuid(),
                ArticleId = article.Id,
                Article = article,
                SavedAt = DateTime.UtcNow
            };

            using var insertCmd = conn.CreateCommand();
            insertCmd.CommandText = "INSERT INTO SavedArticles (Id, UserId, ArticleId, ArticleJson, SavedAt) VALUES (@id, @uid, @aid, @json, @saved)";
            insertCmd.Parameters.AddWithValue("@id", item.Id.ToString());
            insertCmd.Parameters.AddWithValue("@uid", userId.ToString());
            insertCmd.Parameters.AddWithValue("@aid", article.Id.ToString());
            insertCmd.Parameters.AddWithValue("@json", JsonSerializer.Serialize(article));
            insertCmd.Parameters.AddWithValue("@saved", item.SavedAt.ToString("O"));
            insertCmd.ExecuteNonQuery();

            return item;
        }
    }

    public bool Remove(Guid userId, Guid savedItemId)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM SavedArticles WHERE Id = @id AND UserId = @uid";
        cmd.Parameters.AddWithValue("@id", savedItemId.ToString());
        cmd.Parameters.AddWithValue("@uid", userId.ToString());
        return cmd.ExecuteNonQuery() > 0;
    }
}