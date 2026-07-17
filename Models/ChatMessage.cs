namespace RSS_Reader.Models;

public class ChatMessage
{
    public long Id { get; set; }
    public string Role { get; set; } = ""; // "user" or "assistant"
    public string Content { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}
