using Microsoft.Data.Sqlite;
using RSS_Reader.Models;

namespace RSS_Reader.Storage;

public class UserStore : IUserStore
{
    private readonly Database _db;

    public UserStore(Database db)
    {
        _db = db;
        EnsureTable();
    }

    private void EnsureTable()
    {
        using var conn = _db.Open();

        using var createCmd = conn.CreateCommand();
        createCmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS Users (
                Id TEXT PRIMARY KEY,
                Email TEXT NOT NULL UNIQUE,
                PasswordHash TEXT,
                GoogleId TEXT,
                CreatedAt TEXT NOT NULL
            )";
        createCmd.ExecuteNonQuery();

        EnsureColumn(conn, "GoogleId", "TEXT");
        EnsureColumn(conn, "EmailVerified", "INTEGER DEFAULT 0");
        EnsureColumn(conn, "VerificationToken", "TEXT");
    }

    private static void EnsureColumn(SqliteConnection conn, string columnName, string columnDef)
    {
        using var pragmaCmd = conn.CreateCommand();
        pragmaCmd.CommandText = "PRAGMA table_info(Users)";
        using var reader = pragmaCmd.ExecuteReader();
        var exists = false;
        while (reader.Read())
        {
            if (string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
            {
                exists = true;
                break;
            }
        }
        reader.Close();

        if (!exists)
        {
            using var alterCmd = conn.CreateCommand();
            alterCmd.CommandText = $"ALTER TABLE Users ADD COLUMN {columnName} {columnDef}";
            alterCmd.ExecuteNonQuery();
        }
    }

    public List<User> GetAll()
    {
        var users = new List<User>();
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, Email, PasswordHash, GoogleId, CreatedAt, EmailVerified, VerificationToken FROM Users ORDER BY CreatedAt";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            users.Add(ReadUser(reader));
        }
        return users;
    }

    public User? GetByEmail(string email)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, Email, PasswordHash, GoogleId, CreatedAt, EmailVerified, VerificationToken FROM Users WHERE Email = @email";
        cmd.Parameters.AddWithValue("@email", email);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;
        return ReadUser(reader);
    }

    public User? GetById(Guid id)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, Email, PasswordHash, GoogleId, CreatedAt, EmailVerified, VerificationToken FROM Users WHERE Id = @id";
        cmd.Parameters.AddWithValue("@id", id.ToString());
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;
        return ReadUser(reader);
    }

    public User? GetByGoogleId(string googleId)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, Email, PasswordHash, GoogleId, CreatedAt, EmailVerified, VerificationToken FROM Users WHERE GoogleId = @googleId";
        cmd.Parameters.AddWithValue("@googleId", googleId);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;
        return ReadUser(reader);
    }

    public User Create(string email, string? passwordHash = null)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = passwordHash,
            GoogleId = null,
            CreatedAt = DateTime.UtcNow,
            EmailVerified = false,
            VerificationToken = Guid.NewGuid().ToString("N")
        };
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO Users (Id, Email, PasswordHash, GoogleId, CreatedAt, EmailVerified, VerificationToken) VALUES (@id, @email, @hash, @googleId, @created, @verified, @token)";
        cmd.Parameters.AddWithValue("@id", user.Id.ToString());
        cmd.Parameters.AddWithValue("@email", user.Email);
        cmd.Parameters.AddWithValue("@hash", (object?)user.PasswordHash ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@googleId", (object?)user.GoogleId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@created", user.CreatedAt.ToString("O"));
        cmd.Parameters.AddWithValue("@verified", user.EmailVerified ? 1 : 0);
        cmd.Parameters.AddWithValue("@token", (object?)user.VerificationToken ?? DBNull.Value);
        cmd.ExecuteNonQuery();
        return user;
    }

    public User CreateFromGoogle(string email, string googleId)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = null,
            GoogleId = googleId,
            CreatedAt = DateTime.UtcNow,
            EmailVerified = true,
            VerificationToken = null
        };
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO Users (Id, Email, PasswordHash, GoogleId, CreatedAt, EmailVerified, VerificationToken) VALUES (@id, @email, @hash, @googleId, @created, @verified, @token)";
        cmd.Parameters.AddWithValue("@id", user.Id.ToString());
        cmd.Parameters.AddWithValue("@email", user.Email);
        cmd.Parameters.AddWithValue("@hash", DBNull.Value);
        cmd.Parameters.AddWithValue("@googleId", user.GoogleId);
        cmd.Parameters.AddWithValue("@created", user.CreatedAt.ToString("O"));
        cmd.Parameters.AddWithValue("@verified", 1);
        cmd.Parameters.AddWithValue("@token", DBNull.Value);
        cmd.ExecuteNonQuery();
        return user;
    }

    public void UpdatePassword(Guid userId, string passwordHash)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE Users SET PasswordHash = @hash WHERE Id = @id";
        cmd.Parameters.AddWithValue("@hash", passwordHash);
        cmd.Parameters.AddWithValue("@id", userId.ToString());
        cmd.ExecuteNonQuery();
    }

    public void Delete(Guid userId)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM Users WHERE Id = @id";
        cmd.Parameters.AddWithValue("@id", userId.ToString());
        cmd.ExecuteNonQuery();
    }

    public void LinkGoogle(Guid userId, string googleId)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE Users SET GoogleId = @googleId WHERE Id = @id";
        cmd.Parameters.AddWithValue("@googleId", googleId);
        cmd.Parameters.AddWithValue("@id", userId.ToString());
        cmd.ExecuteNonQuery();
    }

    public User? GetByVerificationToken(string token)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, Email, PasswordHash, GoogleId, CreatedAt, EmailVerified, VerificationToken FROM Users WHERE VerificationToken = @token";
        cmd.Parameters.AddWithValue("@token", token);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;
        return ReadUser(reader);
    }

    public void VerifyEmail(string token)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE Users SET EmailVerified = 1, VerificationToken = NULL WHERE VerificationToken = @token";
        cmd.Parameters.AddWithValue("@token", token);
        cmd.ExecuteNonQuery();
    }

    private static User ReadUser(SqliteDataReader reader) => new()
    {
        Id = Guid.Parse(reader.GetString(0)),
        Email = reader.GetString(1),
        PasswordHash = reader.IsDBNull(2) ? null : reader.GetString(2),
        GoogleId = reader.IsDBNull(3) ? null : reader.GetString(3),
        CreatedAt = DateTime.Parse(reader.GetString(4)),
        EmailVerified = reader.GetInt32(5) != 0,
        VerificationToken = reader.IsDBNull(6) ? null : reader.GetString(6)
    };
}