using System.Net; //use it to download raw html
using HtmlAgilityPack; //3rd party library used to parse and manipulate html 

namespace RSS_Reader.Parsing;

public static class ArticleContentCleaner
{
    public static string CleanHtmlSummary(string html)
    {
        var document = new HtmlDocument(); //comes from HTMLagilitypack , its like xmldocument, it builts a tree like h3 p and etc..
        document.LoadHtml(html); //loadhtml means read this html and understand the structure, so it the doc object ends up knows headings, paragraphas ...etc

        var text = document.DocumentNode.InnerText; // like starting point/root of dom, have the whole html page, and innertext is like only the visible text
        text = WebUtility.HtmlDecode(text); //remove html entities as &amp and $apos and stuff
        text = string.Join(" ", text.Split((char[])null, StringSplitOptions.RemoveEmptyEntries)); //split the text into words with removing empty entries and join with spaces
        return text;
    }

    //pulls the src of the first <img> tag out of a chunk of HTML, if any. Used as a last-resort
    //image source for feeds that don't use media:thumbnail/media:content, an image enclosure, or
    //(for JSON Feed) a top-level "image" field.
    public static string? ExtractFirstImgSrc(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return null;

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var imgNode = doc.DocumentNode.SelectSingleNode("//img[@src]");
        var src = imgNode?.GetAttributeValue("src", "");
        return string.IsNullOrWhiteSpace(src) ? null : src;
    }

    public static string? ResolveImageFallback(ParsedItem item, string articleLink, string feedUrl)
    {
        var inlineImageUrl = ExtractFirstImgSrc(item.SummaryHtml) ?? ExtractFirstImgSrc(item.ContentHtml);
        if (inlineImageUrl == null) return null;

        //inline <img src="..."> is frequently a relative path - resolve it against the article's own link, falling back to the feed's URL
        var baseUrl = !string.IsNullOrWhiteSpace(articleLink) ? articleLink : feedUrl;

        return Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri) &&
               Uri.TryCreate(baseUri, inlineImageUrl, out var resolved)
            ? resolved.ToString()
            : inlineImageUrl;
    }
}
