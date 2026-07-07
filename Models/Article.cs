namespace RSS_Reader.Models;

public class Article
{
    public string Title { get; set; } = "";
    public string Link { get; set; } = "";
    public string Summary { get; set; } = "";
    public DateTime PublishDate { get; set; }
    public string FeedTitle { get; set; } = "";
}