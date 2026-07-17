using System.Net.Http.Headers;
using System.Text;
using HtmlAgilityPack;
using RSS_Reader.Models;

namespace RSS_Reader.Services;

public class FeedFetcher : IFeedFetcher
{
    //attaches an Authorization header when the feed has credentials configured.
    //bearer token wins if both are set; otherwise falls back to HTTP Basic with username/password.
    //a feed with no credentials at all gets no Authorization header, same as before this existed.
    private static void ApplyFeedAuth(HttpRequestMessage request, Feed feed)
    {
        if (!string.IsNullOrWhiteSpace(feed.BearerToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", feed.BearerToken);
        }
        else if (!string.IsNullOrWhiteSpace(feed.Username))
        {
            var raw = $"{feed.Username}:{feed.Password}";
            var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(raw));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", encoded);
        }
    }

    public async Task<string> FetchTextAsync(HttpClient httpClient, string url, Feed feed)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.ParseAdd("RSSReader/1.0");
        ApplyFeedAuth(request, feed);

        using var response = await httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var text = await response.Content.ReadAsStringAsync();
        return CleanXml(text);
    }

    private static string CleanXml(string xml)
    {
        if (string.IsNullOrEmpty(xml)) return xml;
        var result = new StringBuilder(xml.Length);
        foreach (var c in xml)
        {
            if (c == 0x9 || c == 0xA || c == 0xD ||
                (c >= 0x20 && c <= 0xD7FF) ||
                (c >= 0xE000 && c <= 0xFFFD))
            {
                result.Append(c);
            }
        }
        return result.ToString();
    }

    public async Task<string?> DiscoverFeedUrlAsync(string pageUrl, Feed feed, HttpClient httpClient)
    {
        try
        {
            var html = await FetchTextAsync(httpClient, pageUrl, feed);
            var document = new HtmlDocument();
            document.LoadHtml(html);

            var linkNode = document.DocumentNode.SelectSingleNode(
                "//link[@rel='alternate'][@type='application/rss+xml' or @type='application/atom+xml']");

            var href = linkNode?.GetAttributeValue("href", "");
            if (string.IsNullOrWhiteSpace(href))
            {
                return null;
            }

            //href might be relative (e.g. "/feed.xml"), resolve it against the page's own URL
            if (Uri.TryCreate(new Uri(pageUrl), href, out var resolvedUri))
            {
                return resolvedUri.ToString();
            }

            return null;
        }
        catch (Exception)
        {
            return null; //not a webpage we could read, or no feed link present - caller treats this the same as "no feed found"
        }
    }
}
