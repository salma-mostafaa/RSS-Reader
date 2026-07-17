namespace RSS_Reader.Models;

public class ArticleSummary
{
    public Guid ArticleId { get; set; }
    public string TextEn { get; set; } = "";
    public string TextAr { get; set; } = "";
    public DateTime GeneratedAt { get; set; }
}
