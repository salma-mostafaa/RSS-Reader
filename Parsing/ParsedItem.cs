namespace RSS_Reader.Parsing;

public class ParsedItem
{
    public string? Title { get; set; }
    public string? Link { get; set; }
    public string? SummaryHtml { get; set; }
    public string? ContentHtml { get; set; }
    public DateTime? PublishDate { get; set; }
    public string? ImageUrl { get; set; }
    public string? EnclosureUrl { get; set; }
    public string? EnclosureType { get; set; }
    public List<string> Categories { get; set; } = new();
}
