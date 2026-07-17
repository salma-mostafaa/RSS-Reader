namespace RSS_Reader.Models;

public class Playlist
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}