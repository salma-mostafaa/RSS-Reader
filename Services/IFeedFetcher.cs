using RSS_Reader.Models;

namespace RSS_Reader.Services;

public interface IFeedFetcher
{
    // Every feed fetch in the app goes through here, so authenticated feeds work everywhere a
    // feed is fetched: adding, discovering, and refreshing.
    Task<string> FetchTextAsync(HttpClient httpClient, string url, Feed feed);

    // Not a feed itself - maybe it's a regular webpage that links to one. Returns the discovered
    // feed URL, or null if this doesn't look like a webpage we could read or it has no feed link.
    Task<string?> DiscoverFeedUrlAsync(string pageUrl, Feed feed, HttpClient httpClient);
}
