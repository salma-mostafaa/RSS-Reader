using Microsoft.Data.Sqlite;
using RSS_Reader.Models;

namespace RSS_Reader.Storage;

public class ArticleSummaryStore : IArticleSummaryStore
{
    private readonly Database _db;

    public ArticleSummaryStore(Database db)
    {
        _db = db;
        EnsureTable();
    }

    private void EnsureTable()
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS ArticleSummaries (
                ArticleId TEXT PRIMARY KEY,
                TextEn TEXT NOT NULL,
                TextAr TEXT NOT NULL,
                GeneratedAt TEXT NOT NULL
            )";
        cmd.ExecuteNonQuery();
    }

    public ArticleSummary? Get(Guid articleId)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT ArticleId, TextEn, TextAr, GeneratedAt FROM ArticleSummaries WHERE ArticleId = @id";
        cmd.Parameters.AddWithValue("@id", articleId.ToString());
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;
        return new ArticleSummary
        {
            ArticleId = Guid.Parse(reader.GetString(0)),
            TextEn = reader.GetString(1),
            TextAr = reader.GetString(2),
            GeneratedAt = DateTime.Parse(reader.GetString(3))
        };
    }

    public void Save(ArticleSummary summary)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO ArticleSummaries (ArticleId, TextEn, TextAr, GeneratedAt) VALUES (@id, @en, @ar, @generated)
            ON CONFLICT(ArticleId) DO UPDATE SET TextEn = @en, TextAr = @ar, GeneratedAt = @generated";
        cmd.Parameters.AddWithValue("@id", summary.ArticleId.ToString());
        cmd.Parameters.AddWithValue("@en", summary.TextEn);
        cmd.Parameters.AddWithValue("@ar", summary.TextAr);
        cmd.Parameters.AddWithValue("@generated", summary.GeneratedAt.ToString("O"));
        cmd.ExecuteNonQuery();
    }
}
