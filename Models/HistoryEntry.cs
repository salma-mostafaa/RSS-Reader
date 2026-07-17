using System.Text.Json.Serialization;

namespace RSS_Reader.Models;

public class HistoryEntry
{
    public Guid Id { get; set; } // the history-entry's own id
    public Guid ArticleId { get; set; }
    public Article Article { get; set; } = new(); // snapshot, same reasoning as SavedArticle/PlaylistItem
    public DateTime ViewedAt { get; set; }

    [JsonIgnore]
    public string ArticleJson
    {
        get => System.Text.Json.JsonSerializer.Serialize(Article);
        set
        {
            if (!string.IsNullOrWhiteSpace(value))
                Article = System.Text.Json.JsonSerializer.Deserialize<Article>(value) ?? new();
        }
    }
}
