using System.Linq;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using RSS_Reader.Models;
using RSS_Reader.Storage;

namespace RSS_Reader.Services;

public class AiSummaryService
{
    private const string Model = "deepseek-chat";
    private static readonly TimeSpan BackgroundInterval = TimeSpan.FromMinutes(10);

    private readonly IArticleStore _articleStore;
    private readonly ISummaryStore _summaryStore;
    private readonly IConfiguration _configuration;

    public AiSummaryService(IArticleStore articleStore, ISummaryStore summaryStore, IConfiguration configuration)
    {
        _articleStore = articleStore;
        _summaryStore = summaryStore;
        _configuration = configuration;
    }

    public async Task<AiSummary> GenerateAsync(HttpClient httpClient, HashSet<Guid>? feedIds = null, string? userId = null)
    {
        var apiKey = _configuration["DEEPSEEK_API_KEY"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("DEEPSEEK_API_KEY is not configured");
        }

        var articles = _articleStore.Load()
            .Where(a => feedIds == null || feedIds.Contains(a.FeedId))
            .OrderByDescending(a => a.PublishDate)
            .Take(20)
            .ToList();

        if (articles.Count == 0)
        {
            throw new InvalidOperationException("No articles available to summarize yet - refresh your feeds first");
        }

        var articleListText = string.Join("\n", articles.Select(a => $"- [{a.FeedTitle}] {a.Title}: {a.Summary}"));

        // Generate BOTH languages in a single call so switching the UI's display language later
        // (English <-> Arabic) never needs another paid API request - it just swaps cached text.
        // Structured as category-grouped bullet points (rather than one mixed paragraph) so the
        // frontend can render clear section headers with short scannable bullets under each.
        // Capped to at most 5 categories with up to 3 points each (in EACH language) - the
        // previous uncapped version could ask for up to 10 categories x 4 points x 2 languages,
        // which routinely exceeded the token budget and got cut off mid-JSON.
        var prompt = "You are summarizing ONLY the articles below from the user's personal RSS feeds. " +
            "Do NOT bring in outside news or general knowledge. Respond with ONLY a valid JSON object " +
            "(no markdown, no code fences, no extra text) with exactly two keys: \"en\" and \"ar\". " +
            "Each of those must be an object with a single key \"sections\", an array of AT MOST 5 section objects. " +
            "Each section object has exactly two keys: " +
            "\"category\" - one of exactly these values: politics, business, technology, sports, health, science, entertainment, environment, world, general; " +
            "\"points\" - an array of 1 to 3 short, concise bullet-point sentences (each a standalone fact or story, at most about 15 words) covering what's happening in that category based on the articles below. " +
            "Only include a category if the articles actually contain relevant content for it - do not force categories with nothing to say. " +
            "Order the sections from most to least covered. " +
            "The \"ar\" value must contain the exact same categories in the exact same order, translated into natural, fluent Arabic (translate the point text only - the \"category\" value itself must stay as the exact English word from the list above in both \"en\" and \"ar\", since it's used as a lookup key, not displayed directly).\n\n" +
            articleListText;

        var requestBody = new
        {
            model = Model,
            max_tokens = 3500,
            response_format = new { type = "json_object" },
            messages = new[]
            {
                new { role = "user", content = prompt }
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.deepseek.com/v1/chat/completions");
        request.Headers.Add("Authorization", $"Bearer {apiKey}");
        request.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

        using var response = await httpClient.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"DeepSeek API call failed ({(int)response.StatusCode}): {errorBody}");
        }

        using var responseDoc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var choice = responseDoc.RootElement.GetProperty("choices")[0];

        // finish_reason "length" means DeepSeek cut the response short because it hit
        // max_tokens - the authoritative signal that this response is truncated, rather than
        // waiting to discover it indirectly when JSON parsing fails on the incomplete text.
        var finishReason = choice.TryGetProperty("finish_reason", out var frEl) ? frEl.GetString() : null;
        if (finishReason == "length")
        {
            throw new Exception("The AI response was cut off before it finished (ran out of token budget) - please try regenerating.");
        }

        var rawContent = choice
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? "";

        var (sectionsEn, sectionsAr) = ParseBilingualSummary(rawContent);
        var textEn = FlattenSections(sectionsEn);
        var textAr = FlattenSections(sectionsAr);

        var summary = new AiSummary
        {
            Text = textEn, // kept for backward compatibility with any older client
            TextEn = textEn,
            TextAr = textAr,
            SectionsEn = sectionsEn,
            SectionsAr = sectionsAr,
            GeneratedAt = DateTime.UtcNow,
            ArticleCount = articles.Count
        };

        _summaryStore.Save(summary, userId);
        return summary;
    }

    private static readonly string[] ValidCategories =
    {
        "politics", "business", "technology", "sports", "health",
        "science", "entertainment", "environment", "world", "general"
    };

    // The model is asked to return raw JSON, but models sometimes wrap it in ```json fences
    // regardless - strip those defensively before parsing. If the response still isn't valid
    // JSON (most commonly because it got cut off mid-generation by hitting the token limit),
    // this throws rather than silently saving the broken raw text as the new "summary" -
    // that would overwrite the last good cached summary with garbage the user would see.
    private static (List<SummarySection> en, List<SummarySection> ar) ParseBilingualSummary(string rawContent)
    {
        var cleaned = rawContent.Trim();
        if (cleaned.StartsWith("```"))
        {
            var firstNewline = cleaned.IndexOf('\n');
            if (firstNewline >= 0) cleaned = cleaned[(firstNewline + 1)..];
            if (cleaned.EndsWith("```")) cleaned = cleaned[..^3];
            cleaned = cleaned.Trim();
        }

        // Defensive: if the model wrapped the JSON in any stray prose, pull out just the
        // {...} block before parsing.
        var firstBrace = cleaned.IndexOf('{');
        var lastBrace = cleaned.LastIndexOf('}');
        if (firstBrace >= 0 && lastBrace > firstBrace)
        {
            cleaned = cleaned[firstBrace..(lastBrace + 1)];
        }

        try
        {
            using var doc = JsonDocument.Parse(cleaned);
            var en = ParseSections(doc.RootElement, "en");
            var ar = ParseSections(doc.RootElement, "ar");
            return (en, ar);
        }
        catch (JsonException ex)
        {
            throw new Exception(
                "The AI response could not be read as valid summary data (it may have been cut off) - please try regenerating.",
                ex);
        }
    }

    private static List<SummarySection> ParseSections(JsonElement root, string lang)
    {
        var result = new List<SummarySection>();
        if (!root.TryGetProperty(lang, out var langEl)) return result;
        if (!langEl.TryGetProperty("sections", out var sectionsEl) || sectionsEl.ValueKind != JsonValueKind.Array) return result;

        foreach (var sectionEl in sectionsEl.EnumerateArray())
        {
            var category = sectionEl.TryGetProperty("category", out var catEl)
                ? (catEl.GetString() ?? "general").Trim().ToLowerInvariant()
                : "general";
            if (!ValidCategories.Contains(category)) category = "general";

            var points = new List<string>();
            if (sectionEl.TryGetProperty("points", out var pointsEl) && pointsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var p in pointsEl.EnumerateArray())
                {
                    var text = p.GetString();
                    if (!string.IsNullOrWhiteSpace(text)) points.Add(text.Trim());
                }
            }
            if (points.Count > 0) result.Add(new SummarySection { Category = category, Points = points });
        }
        return result;
    }

    // Plain-text rendering of the structured sections, used for the `Text`/`TextEn`/`TextAr`
    // backward-compatible fields and as a fallback if the frontend ever needs raw text (e.g. copy).
    private static string FlattenSections(List<SummarySection> sections)
    {
        return string.Join("\n\n", sections.Select(s =>
            s.Category.ToUpperInvariant() + ":\n" + string.Join("\n", s.Points.Select(p => "- " + p))));
    }

    public void StartDailyBackgroundLoop()
    {
        var apiKey = _configuration["DEEPSEEK_API_KEY"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            using var timer = new PeriodicTimer(BackgroundInterval);
            while (true)
            {
                try
                {
                    using var backgroundHttpClient = new HttpClient();
                    await GenerateAsync(backgroundHttpClient);
                }
                catch
                {
                }

                await timer.WaitForNextTickAsync();
            }
        });
    }
}