using System.Text.Json;
using Microsoft.Data.Sqlite;
using RSS_Reader.Models;

namespace RSS_Reader.Storage;

public class HistoryStore : IHistoryStore
{
    private readonly Database _db;
    private readonly object _lock = new();

    public HistoryStore(Database db)
    {
        _db = db;
        EnsureTable();
    }

    private const string CreateTableSql = @"
        CREATE TABLE ReadHistory (
            Id TEXT PRIMARY KEY,
            UserId TEXT NOT NULL,
            ArticleId TEXT NOT NULL,
            ArticleJson TEXT NOT NULL,
            ViewedAt TEXT NOT NULL,
            UNIQUE(UserId, ArticleId)
        )";

    private void EnsureTable()
    {
        using var conn = _db.Open();

        using var existsCmd = conn.CreateCommand();
        existsCmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='ReadHistory'";
        var tableExists = existsCmd.ExecuteScalar() != null;

        if (!tableExists)
        {
            using var createCmd = conn.CreateCommand();
            createCmd.CommandText = CreateTableSql;
            createCmd.ExecuteNonQuery();
            return;
        }

        // Table already existed from before user accounts did. Its old UNIQUE(ArticleId)
        // constraint (global uniqueness, not per-user) can't be altered away with a simple
        // ALTER TABLE in SQLite, so if it's missing the new UserId column, rebuild it fresh
        // instead. The old rows have no real owner to assign them to, so they're dropped
        // rather than guessed at - this is reading history, not something worth risking a
        // wrong-owner assignment over.
        using var pragmaCmd = conn.CreateCommand();
        pragmaCmd.CommandText = "PRAGMA table_info(ReadHistory)";
        using var reader = pragmaCmd.ExecuteReader();
        var hasUserId = false;
        while (reader.Read())
        {
            if (string.Equals(reader.GetString(1), "UserId", StringComparison.OrdinalIgnoreCase)) { hasUserId = true; break; }
        }
        reader.Close();

        if (!hasUserId)
        {
            using var tx = conn.BeginTransaction();
            using var dropCmd = conn.CreateCommand();
            dropCmd.CommandText = "DROP TABLE ReadHistory";
            dropCmd.ExecuteNonQuery();

            using var createCmd = conn.CreateCommand();
            createCmd.CommandText = CreateTableSql;
            createCmd.ExecuteNonQuery();
            tx.Commit();
        }
    }

    public List<HistoryEntry> LoadAll(Guid userId)
    {
        var items = new List<HistoryEntry>();
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, ArticleId, ArticleJson, ViewedAt FROM ReadHistory WHERE UserId = @uid ORDER BY ViewedAt DESC";
        cmd.Parameters.AddWithValue("@uid", userId.ToString());
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var entry = new HistoryEntry
            {
                Id = Guid.Parse(reader.GetString(0)),
                ArticleId = Guid.Parse(reader.GetString(1)),
                ViewedAt = DateTime.Parse(reader.GetString(3))
            };
            entry.ArticleJson = reader.GetString(2);
            items.Add(entry);
        }
        return items;
    }

    public List<Guid> LoadViewedArticleIds(Guid userId)
    {
        var ids = new List<Guid>();
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT ArticleId FROM ReadHistory WHERE UserId = @uid";
        cmd.Parameters.AddWithValue("@uid", userId.ToString());
        using var reader = cmd.ExecuteReader();
        while (reader.Read()) ids.Add(Guid.Parse(reader.GetString(0)));
        return ids;
    }

    // Viewing an already-logged article bumps its ViewedAt to now (like browser history)
    // rather than accumulating duplicate rows for the same article.
    public HistoryEntry LogView(Guid userId, Article article)
    {
        lock (_lock)
        {
            using var conn = _db.Open();
            var viewedAt = DateTime.UtcNow;

            using var checkCmd = conn.CreateCommand();
            checkCmd.CommandText = "SELECT Id FROM ReadHistory WHERE UserId = @uid AND ArticleId = @aid";
            checkCmd.Parameters.AddWithValue("@uid", userId.ToString());
            checkCmd.Parameters.AddWithValue("@aid", article.Id.ToString());
            var existingId = checkCmd.ExecuteScalar() as string;

            if (existingId != null)
            {
                using var updateCmd = conn.CreateCommand();
                updateCmd.CommandText = "UPDATE ReadHistory SET ViewedAt = @viewed, ArticleJson = @json WHERE Id = @id";
                updateCmd.Parameters.AddWithValue("@viewed", viewedAt.ToString("O"));
                updateCmd.Parameters.AddWithValue("@json", JsonSerializer.Serialize(article));
                updateCmd.Parameters.AddWithValue("@id", existingId);
                updateCmd.ExecuteNonQuery();
                return new HistoryEntry { Id = Guid.Parse(existingId), ArticleId = article.Id, Article = article, ViewedAt = viewedAt };
            }

            var entry = new HistoryEntry { Id = Guid.NewGuid(), ArticleId = article.Id, Article = article, ViewedAt = viewedAt };
            using var insertCmd = conn.CreateCommand();
            insertCmd.CommandText = "INSERT INTO ReadHistory (Id, UserId, ArticleId, ArticleJson, ViewedAt) VALUES (@id, @uid, @aid, @json, @viewed)";
            insertCmd.Parameters.AddWithValue("@id", entry.Id.ToString());
            insertCmd.Parameters.AddWithValue("@uid", userId.ToString());
            insertCmd.Parameters.AddWithValue("@aid", article.Id.ToString());
            insertCmd.Parameters.AddWithValue("@json", JsonSerializer.Serialize(article));
            insertCmd.Parameters.AddWithValue("@viewed", viewedAt.ToString("O"));
            insertCmd.ExecuteNonQuery();
            return entry;
        }
    }

    public void Clear(Guid userId)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM ReadHistory WHERE UserId = @uid";
        cmd.Parameters.AddWithValue("@uid", userId.ToString());
        cmd.ExecuteNonQuery();
    }
}