namespace RSS_Reader.Models; //organize class that belongs to models folder

public class Article
{
    public Guid Id { get; set; } //deterministic hash of feed+link, so the same real-world article always gets the same Id across refreshes
    public Guid FeedId { get; set; } //which feed this came from - used for retention cleanup and to link back to the subscription
    public string Title { get; set; } = "";
    public string Link { get; set; } = "";
    public string Summary { get; set; } = "";
    public DateTime PublishDate { get; set; }
    public DateTime FetchedAt { get; set; } //when we first stored this article - retention is based on this, not PublishDate
    public string FeedTitle { get; set; } = "";
    public string? ImageUrl { get; set; }
    public string? EnclosureUrl { get; set; }
    public string? EnclosureType { get; set; }
    public bool IsRtl { get; set; } //true when the title/summary text is majority right-to-left (Arabic, Hebrew, Urdu, etc.) - frontend uses this to set dir="rtl" per-card
    public List<string> Categories { get; set; } = new();
}