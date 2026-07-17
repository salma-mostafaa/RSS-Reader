namespace RSS_Reader.Models;

public class Feed
{
    public Guid Id { get; set; }
    public string Url { get; set; } = "";
    public string Title { get; set; } = "";
    public string? UserId { get; set; }

    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? BearerToken { get; set; }
}