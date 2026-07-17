using RSS_Reader.Models;
using RSS_Reader.Parsing;

namespace RSS_Reader.Services;

// The result of trying to add a new feed: either it validated (with its title filled in and,
// if the pasted URL turned out to be a webpage, its Url swapped for the discovered feed URL)
// and comes with the articles it already has, or it failed with a message suitable to show
// the person who pasted the URL.
public record FeedAddResult(bool Success, string? Error, List<Article> InitialArticles);

public class FeedService
{
    private readonly IFeedFetcher _feedFetcher;

    public FeedService(IFeedFetcher feedFetcher)
    {
        _feedFetcher = feedFetcher;
    }

    public List<Article> BuildArticlesFromParsedFeed(Feed feed, ParsedFeed parsedFeed)
    {
        var articles = new List<Article>();

        foreach (var item in parsedFeed.Items)
        {
            var article = new Article();

            article.Title = string.IsNullOrWhiteSpace(item.Title) ? "(untitled)" : item.Title!; //some malformed feed items have no title, which used to throw and skip the whole feed
            article.Link = item.Link ?? "";
            article.Summary = ArticleContentCleaner.CleanHtmlSummary(item.SummaryHtml ?? "");
            article.ImageUrl = item.ImageUrl ?? ArticleContentCleaner.ResolveImageFallback(item, article.Link, feed.Url);
            article.EnclosureUrl = item.EnclosureUrl;
            article.EnclosureType = item.EnclosureType;
            article.PublishDate = item.PublishDate ?? DateTime.UtcNow; //no date on the item - treat it as "just now" rather than sorting it to the bottom forever
            article.FeedTitle = feed.Title;
            article.FeedId = feed.Id;
            article.FetchedAt = DateTime.UtcNow;
            article.Id = ArticleIdGenerator.Generate(feed.Id, article.Link, article.Title);
            article.Categories = item.Categories ?? new List<string>();
            article.IsRtl = RtlDetector.IsRtl(article.Title + " " + article.Summary);

            articles.Add(article);
        }

        return articles;
    }

    // Shared by /articles/refresh-all and the per-feed refresh endpoint - fetching and building
    // are the network-dependent part; persistence is the article store's job, not this service's.
    public async Task<List<Article>> FetchArticlesForFeedAsync(Feed feed, HttpClient httpClient)
    {
        try
        {
            var text = await _feedFetcher.FetchTextAsync(httpClient, feed.Url, feed);
            var parsedFeed = FeedParser.Parse(text);
            return BuildArticlesFromParsedFeed(feed, parsedFeed);
        }
        catch (Exception)
        {
            return new List<Article>(); // leave articles empty for this feed if it fails
        }
    }

    // Validates a newly-submitted feed: tries to fetch+parse it directly, and if that fails,
    // falls back to treating the URL as a webpage and looking for a <link rel="alternate"> feed
    // link on it. Mutates feed.Title (and feed.Url, if discovery kicked in) on success.
    public async Task<FeedAddResult> AddFeedAsync(Feed feed, HttpClient httpClient)
    {
        ParsedFeed parsedFeed;

        try
        {
            var text = await _feedFetcher.FetchTextAsync(httpClient, feed.Url, feed);
            Console.WriteLine($"[FEED] Fetched {feed.Url}: {text.Length} chars, starts with: {text.Substring(0, Math.Min(80, text.Length))}");
            parsedFeed = FeedParser.Parse(text);
            feed.Title = string.IsNullOrWhiteSpace(parsedFeed.Title) ? feed.Url : parsedFeed.Title;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[FEED] Direct parse failed for {feed.Url}: {ex.Message}");
            var discoveredUrl = await _feedFetcher.DiscoverFeedUrlAsync(feed.Url, feed, httpClient);

            if (discoveredUrl == null)
            {
                Console.WriteLine($"[FEED] No feed URL discovered at {feed.Url}");
                return new FeedAddResult(false, "Invalid Rss, Atom, or JSON feed", new List<Article>());
            }

            Console.WriteLine($"[FEED] Discovered feed URL: {discoveredUrl}");
            try
            {
                var text = await _feedFetcher.FetchTextAsync(httpClient, discoveredUrl, feed);
                parsedFeed = FeedParser.Parse(text);
                feed.Title = string.IsNullOrWhiteSpace(parsedFeed.Title) ? discoveredUrl : parsedFeed.Title;
                feed.Url = discoveredUrl;
            }
            catch (Exception ex2)
            {
                Console.WriteLine($"[FEED] Discovered feed parse failed for {discoveredUrl}: {ex2.Message}");
                return new FeedAddResult(false, "Invalid Rss, Atom, or JSON feed", new List<Article>());
            }
        }

        //we already have the parsed feed in hand from the validation step above, so build its
        //articles directly instead of fetching the same URL a second time
        var initialArticles = BuildArticlesFromParsedFeed(feed, parsedFeed);
        return new FeedAddResult(true, null, initialArticles);
    }
}
