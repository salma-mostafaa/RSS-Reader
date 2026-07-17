using System.ServiceModel.Syndication; //handles web feeds , provide classes to read write, manipulate rss,...
using System.Text.Json; // handle json dat
using System.Xml; //xml processing
using System.Xml.Linq; // modern xml proceing 

namespace RSS_Reader.Parsing;

public static class FeedParser //main parser, everything eventually comes here
{
    public static ParsedFeed Parse(string rawText) //raw rss xml/json feed
    {
        var trimmed = rawText.TrimStart();

        if (trimmed.StartsWith("{"))
        {
            return ParseJsonFeed(rawText); //throws on genuinely malformed JSON, caught by the caller same as bad XML was
        }

        return ParseXmlFeed(rawText);
    }

    private static ParsedFeed ParseXmlFeed(string rawText) 
    {
        using var stringReader = new StringReader(rawText);
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Ignore,
            CheckCharacters = false
        };
        using var xmlReader = XmlReader.Create(stringReader, settings);
        var rssFeed = SyndicationFeed.Load(xmlReader) ?? throw new InvalidOperationException("Feed was empty");
        //using microsoft's syndicationfeed library
        return new ParsedFeed
        {
            Title = rssFeed.Title?.Text,
            Items = rssFeed.Items.Select(MapSyndicationItem).ToList() //mapsyndicationitem-> converts every XML article into your ParsedItem.
        };
    }

    private static ParsedItem MapSyndicationItem(SyndicationItem item)
    {
        var parsed = new ParsedItem
        {
            Title = item.Title?.Text,
            Link = item.Links.FirstOrDefault()?.Uri.ToString(),
            SummaryHtml = item.Summary?.Text,
            ContentHtml = (item.Content as TextSyndicationContent)?.Text,
            PublishDate = item.PublishDate.DateTime
        };

        // Tier 1: media:thumbnail / media:content extension tags (most reliable when present)
        foreach (var ext in item.ElementExtensions)
        {
            if (ext.OuterName == "thumbnail" || ext.OuterName == "content")
            {
                var el = ext.GetObject<XElement>();
                var url = el.Attribute("url")?.Value;
                if (!string.IsNullOrWhiteSpace(url))
                {
                    parsed.ImageUrl = url;
                    break;
                }
            }
        }

        // Categories
        foreach (var cat in item.Categories)
        {
            if (!string.IsNullOrWhiteSpace(cat.Name))
                parsed.Categories.Add(cat.Name);
        }

        // Tier 2: an enclosure whose type is actually an image (some feeds attach the cover image this way)
        if (parsed.ImageUrl == null)
        {
            var imageEnclosure = item.Links.FirstOrDefault(l =>
                l.RelationshipType == "enclosure" &&
                l.MediaType != null &&
                l.MediaType.StartsWith("image/"));

            if (imageEnclosure != null)
            {
                parsed.ImageUrl = imageEnclosure.Uri.ToString();
            }
        }

        var enclosure = item.Links.FirstOrDefault(l =>
            l.RelationshipType == "enclosure" &&
            l.MediaType != null &&
            (l.MediaType.StartsWith("audio/") || l.MediaType.StartsWith("video/")));

        if (enclosure != null)
        {
            parsed.EnclosureUrl = enclosure.Uri.ToString();
            parsed.EnclosureType = enclosure.MediaType;
        }

        return parsed;
    }

    private static ParsedFeed ParseJsonFeed(string rawText)
    {
        using var doc = JsonDocument.Parse(rawText);
        var root = doc.RootElement;

        var feed = new ParsedFeed
        {
            Title = GetJsonString(root, "title")
        };

        if (!root.TryGetProperty("items", out var itemsEl) || itemsEl.ValueKind != JsonValueKind.Array)
        {
            return feed; //no "items" array - return an (empty) feed rather than throwing, so a title-only response still counts as "found a feed"
        }

        foreach (var itemEl in itemsEl.EnumerateArray())
        {
            var item = new ParsedItem
            {
                Title = GetJsonString(itemEl, "title"),
                Link = GetJsonString(itemEl, "url"),
                SummaryHtml = GetJsonString(itemEl, "summary"),
                ContentHtml = GetJsonString(itemEl, "content_html") ?? GetJsonString(itemEl, "content_text"),
                ImageUrl = GetJsonString(itemEl, "image")
            };

            var dateText = GetJsonString(itemEl, "date_published");
            if (dateText != null && DateTime.TryParse(dateText, null, System.Globalization.DateTimeStyles.RoundtripKind, out var parsedDate))
            {
                item.PublishDate = parsedDate;
            }

            //"attachments" is JSON Feed's equivalent of an RSS enclosure - take the first audio/video one
            if (itemEl.TryGetProperty("attachments", out var attachmentsEl) && attachmentsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var attachment in attachmentsEl.EnumerateArray())
                {
                    var mimeType = GetJsonString(attachment, "mime_type");
                    if (mimeType != null && (mimeType.StartsWith("audio/") || mimeType.StartsWith("video/")))
                    {
                        item.EnclosureUrl = GetJsonString(attachment, "url");
                        item.EnclosureType = mimeType;
                        break;
                    }
                }
            }

            feed.Items.Add(item);
        }

        return feed;
    }

    private static string? GetJsonString(JsonElement element, string propertyName) //This helper safely retrieves a string property from a JSON object.
    {
        return element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String
            ? prop.GetString()
            : null;
    }
}
