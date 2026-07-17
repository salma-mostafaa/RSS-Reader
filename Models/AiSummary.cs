namespace RSS_Reader.Models;

// Add the properties/class below to your existing Models/AiSummary.cs (keep whatever else
// is already there) - these are the new structured, category-grouped fields.
public class AiSummary
{
    public string Text { get; set; } = ""; // kept for backward compatibility - flat rendering of SectionsEn
    public string TextEn { get; set; } = "";
    public string TextAr { get; set; } = "";
    public List<SummarySection> SectionsEn { get; set; } = new();
    public List<SummarySection> SectionsAr { get; set; } = new();
    public DateTime GeneratedAt { get; set; }
    public int ArticleCount { get; set; }
}

public class SummarySection
{
    public string Category { get; set; } = ""; // one of: politics, business, technology, sports, health, science, entertainment, environment, world, general
    public List<string> Points { get; set; } = new();
}