using System.Text.Json;
using System.Xml;
using RSS_Reader.Models;
using System.ServiceModel.Syndication;
using HtmlAgilityPack;
using System.Net;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpClient();
var app = builder.Build();

List<Feed> LoadFeeds()
{
    if (!File.Exists("feeds.json"))
    {
        return new List<Feed>();
    }
    var json = File.ReadAllText("feeds.json");
    // File class is from system.io and gives read, write, delete...etc 
    return JsonSerializer.Deserialize<List<Feed>>(json) ?? new List<Feed>(); //only feed objects are allowed 
    // When returning C# objects, ASP.NET Core automatically serializes them to JSON in the HTTP response.

}
app.MapGet("/feeds", () => LoadFeeds());

app.MapPost("/feeds", async (Feed feed, HttpClient httpClient) => //Feed feed -> expect the user to send a feed object
{
    var feeds = LoadFeeds();
    feed.Id = Guid.NewGuid();
    feed.Url = feed.Url?.Trim();


    if (!Uri.TryCreate(feed.Url, UriKind.Absolute, out _))
    {
        return Results.BadRequest("Invalid URL");
    }

    if (feeds.Any(f => f.Url == feed.Url)) //check duplicates first, before wasting a network call on a feed we already have
    {
        return Results.BadRequest("Feed already exists");
    }

    try
    {
        var xml = await httpClient.GetStringAsync(feed.Url); //HttpClient sends a GET request to the feed.url
        using var stringReader = new StringReader(xml); // using here means Create this object, and when we're done with it, automatically clean it up.
        using var xmlReader = XmlReader.Create(stringReader); // we create xml reader It understands XML structure.

        var rssFeed = SyndicationFeed.Load(xmlReader);
        feed.Title = rssFeed?.Title?.Text ?? feed.Url;
    }
    catch (Exception)
    {
        return Results.BadRequest("Invalid Rss or Atom feed");
    }

    feeds.Add(feed);
    SaveFeeds(feeds);
    return Results.Ok(feed); //sends http status 200 ok
});

void SaveFeeds(List<Feed> feeds)
{
    var json = JsonSerializer.Serialize(
        feeds,
        new JsonSerializerOptions
        {
            WriteIndented = true //not everything on one line
        });
    File.WriteAllText("feeds.json", json);
}
async Task<List<Article>> FetchArticlesForFeed(Feed feed, HttpClient httpClient) //shared by /articles and the per-feed refresh endpoint so both stay in sync
{
    var articles = new List<Article>();

    try
    {
        var xml = await httpClient.GetStringAsync(feed.Url);
        using var stringReader = new StringReader(xml); // using here means Create this object, and when we're done with it, automatically clean it up.
        using var xmlReader = XmlReader.Create(stringReader); // we create xml reader It understands XML structure.
        var rssFeed = SyndicationFeed.Load(xmlReader);

        if (rssFeed == null) return articles;

        foreach (var item in rssFeed.Items)
        {
            var article = new Article();

            article.Title = item.Title?.Text ?? "(untitled)"; //item.Title can be null on some malformed feed items, which used to throw and skip the whole feed
            article.Link = item.Links.FirstOrDefault()?.Uri.ToString() ?? ""; //give me the first link, uri represnts the web address

            var summary = item.Summary?.Text ?? ""; //summary is an object

            var document = new HtmlDocument(); //comes from HTMLagilitypack , its like xmldocument, it builts a tree like h3 p and etc..
            document.LoadHtml(summary); //loadhtml means read this html and understand the structure, so it the doc object ends up knows headings, paragraphas ...etc

            summary = document.DocumentNode.InnerText;// like starting point/root of dom, have the whole html page, and innertext is like only the visible text
            summary = WebUtility.HtmlDecode(summary); //remove html entities as &amp and $apos and stuff
            summary = string.Join(" ", summary.Split((char[])null,StringSplitOptions.RemoveEmptyEntries)); //split the text into words with removing empty entries and join with spaces
            //summary = summary.Replace("View Entire Post", "").Trim();

            article.Summary = summary; //finally the cleaned text gets stored inside article

            article.PublishDate = item.PublishDate.DateTime;
            article.FeedTitle = feed.Title;

            articles.Add(article);
        }
    }
    catch (Exception)
    {
        // leave articles empty for this feed if it fails
    }

    return articles;
}

app.MapGet("/articles", async (HttpClient httpClient) =>
{
    var feeds = LoadFeeds();

    List<Article> articles = new();

    foreach (var feed in feeds)
    {
        articles.AddRange(await FetchArticlesForFeed(feed, httpClient));
    }
    articles = articles.OrderByDescending(a => a.PublishDate).ToList();
    return Results.Ok(articles);
});

app.MapPost("/feeds/{id}/refresh", async (Guid id, HttpClient httpClient) => //manually trigger a refresh of one specific feed
{
    var feeds = LoadFeeds();
    var feed = feeds.FirstOrDefault(f => f.Id == id);

    if (feed == null)
    {
        return Results.NotFound();
    }

    var articles = await FetchArticlesForFeed(feed, httpClient);
    articles = articles.OrderByDescending(a => a.PublishDate).ToList();
    return Results.Ok(articles);
});

app.MapDelete("/feeds/{id}", (Guid id) =>
{
    var feeds = LoadFeeds();
    var feed = feeds.FirstOrDefault(f => f.Id == id); //give me the first feed that matches, look ar each feed if feed.id == id , that's the one

    if (feed == null)
    {
        return Results.NotFound(); // return http 404 not found

    }
    feeds.Remove(feed);
    SaveFeeds(feeds);
    return Results.NoContent(); // send 204 no content which is the usual response for Delete
});

app.UseDefaultFiles(); //doesnt need to type index.html manually
app.UseStaticFiles(); //for the index.html

app.Run();
