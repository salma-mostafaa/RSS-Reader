using System.Text.Json.Serialization;

namespace RSS_Reader.Models;

public class PlaylistItem
{
    public Guid Id { get; set; } //the playlist entry's own id - separate from the article's id, so the same article could theoretically be removed and re-added cleanly
    public Guid PlaylistId { get; set; }
    public Guid ArticleId { get; set; }
    public Article Article { get; set; } = new(); //a full snapshot taken when added, so this survives even after the original article ages out of articles.json's retention window
    public DateTime AddedAt { get; set; }

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
