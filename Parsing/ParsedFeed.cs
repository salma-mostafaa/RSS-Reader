namespace RSS_Reader.Parsing;
 //parsing take raw rss/atom/json feed data and convert it into something the application can understand
public class ParsedFeed// temp class representing rss feed before its converted into the feed/article models
{
    public string? Title { get; set; } // stores feed title, but also can be null
    public List<ParsedItem> Items { get; set; } = new(); //items is a list of parseditem object which is articles
}
