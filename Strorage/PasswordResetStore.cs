using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using RSS_Reader.Models;

namespace RSS_Reader.Storage;

public class PasswordResetStore : IPasswordResetStore
{
    private readonly Database _db;
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(1);

    public PasswordResetStore(Database db)
    {
        _db = db;
        EnsureTable();
    }

    private void EnsureTable()
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS PasswordResetTokens (
                Token TEXT PRIMARY KEY,
                UserId TEXT NOT NULL,
                ExpiresAt TEXT NOT NULL,
                Used INTEGER NOT NULL DEFAULT 0
            )";
        cmd.ExecuteNonQuery();
    }

    public PasswordResetToken Create(Guid userId)
    {
        // A random URL-safe token, not a sequential/guessable id - this is effectively the
        // password to reset the password, so it needs the same care as anything sensitive.
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace("+", "-").Replace("/", "_").Replace("=", "");

        var entry = new PasswordResetToken
        {
            Token = token,
            UserId = userId,
            ExpiresAt = DateTime.UtcNow.Add(TokenLifetime),
            Used = false
        };

        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO PasswordResetTokens (Token, UserId, ExpiresAt, Used) VALUES (@token, @uid, @expires, 0)";
        cmd.Parameters.AddWithValue("@token", entry.Token);
        cmd.Parameters.AddWithValue("@uid", userId.ToString());
        cmd.Parameters.AddWithValue("@expires", entry.ExpiresAt.ToString("O"));
        cmd.ExecuteNonQuery();
        return entry;
    }

    public PasswordResetToken? Get(string token)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Token, UserId, ExpiresAt, Used FROM PasswordResetTokens WHERE Token = @token";
        cmd.Parameters.AddWithValue("@token", token);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;
        return new PasswordResetToken
        {
            Token = reader.GetString(0),
            UserId = Guid.Parse(reader.GetString(1)),
            ExpiresAt = DateTime.Parse(reader.GetString(2)),
            Used = reader.GetInt64(3) != 0
        };
    }

    public void MarkUsed(string token)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE PasswordResetTokens SET Used = 1 WHERE Token = @token";
        cmd.Parameters.AddWithValue("@token", token);
        cmd.ExecuteNonQuery();
    }
}
