namespace RSS_Reader.Models;

public class Feed
{
    public Guid Id { get; set; } //globally unique identifier
    public string Url { get; set; } = "";
    public string Title { get; set; } = "";
}