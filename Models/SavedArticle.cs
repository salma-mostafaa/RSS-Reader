using System.Text.Json.Serialization;

namespace RSS_Reader.Models;

public class SavedArticle
{
    public Guid Id { get; set; } // the saved-entry's own id, separate from the article's id
    public Guid ArticleId { get; set; }
    public Article Article { get; set; } = new(); // full snapshot taken at save time, so this survives after the article ages out of articles.json's retention window (same reasoning as PlaylistItem)
    public DateTime SavedAt { get; set; }

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
