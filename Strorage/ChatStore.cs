using Microsoft.Data.Sqlite;
using RSS_Reader.Models;

namespace RSS_Reader.Storage;

public class ChatStore : IChatStore
{
    private readonly Database _db;

    public ChatStore(Database db)
    {
        _db = db;
        EnsureUserIdColumn();
    }

    // ChatMessages already existed from before user accounts did (created without a UserId
    // column at all - that's the actual bug being fixed here: every visitor, signed in or
    // not, was reading and writing the exact same global row set). Adds the missing column
    // on startup if needed - safe to run every time since it checks first.
    private void EnsureUserIdColumn()
    {
        using var conn = _db.Open();
        using var checkCmd = conn.CreateCommand();
        checkCmd.CommandText = "PRAGMA table_info(ChatMessages)";
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
            alterCmd.CommandText = "ALTER TABLE ChatMessages ADD COLUMN UserId TEXT NOT NULL DEFAULT ''";
            alterCmd.ExecuteNonQuery();
        }
    }

    public List<ChatMessage> LoadMessages(Guid userId)
    {
        var messages = new List<ChatMessage>();
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, Role, Content, CreatedAt FROM ChatMessages WHERE UserId = @uid ORDER BY Id";
        cmd.Parameters.AddWithValue("@uid", userId.ToString());
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            messages.Add(new ChatMessage
            {
                Id = reader.GetInt64(0),
                Role = reader.GetString(1),
                Content = reader.GetString(2),
                CreatedAt = DateTime.Parse(reader.GetString(3))
            });
        }
        return messages;
    }

    public void AddMessage(Guid userId, ChatMessage message)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO ChatMessages (UserId, Role, Content, CreatedAt) VALUES (@uid, @role, @content, @created)";
        cmd.Parameters.AddWithValue("@uid", userId.ToString());
        cmd.Parameters.AddWithValue("@role", message.Role);
        cmd.Parameters.AddWithValue("@content", message.Content);
        cmd.Parameters.AddWithValue("@created", message.CreatedAt.ToString("O"));
        cmd.ExecuteNonQuery();
    }

    public void Clear(Guid userId)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM ChatMessages WHERE UserId = @uid";
        cmd.Parameters.AddWithValue("@uid", userId.ToString());
        cmd.ExecuteNonQuery();
    }
}