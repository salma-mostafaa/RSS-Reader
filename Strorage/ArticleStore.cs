using System.Text.Json;
using RSS_Reader.Models;

namespace RSS_Reader.Storage;

public class ArticleStore : IArticleStore
{
    private const string FilePath = "articles.json";

    // How many days a fetched article stays in articles.json before it's pruned, regardless of
    // whether the source feed still serves it. Change this single number to adjust retention
    // ("two weeks or months" - defaulting to two weeks; bump to e.g. 60 for ~2 months).
    public const int RetentionDays = 14;

    // Registered as a singleton (see Program.cs), so one lock instance genuinely guards every
    // concurrent request that touches articles.json - that's the whole point of it living here
    // as an instance field instead of a local variable.
    private readonly object _lock = new();

    public List<Article> Load()
    {
        if (!File.Exists(FilePath))
        {
            return new List<Article>();
        }
        try
        {
            var json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<List<Article>>(json) ?? new List<Article>();
        }
        catch
        {
            return new List<Article>();
        }
    }

    private void Save(List<Article> articles)
    {
        var json = JsonSerializer.Serialize(articles, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(FilePath, json);
    }

    public List<Article> MergeAndPersist(Guid feedId, List<Article> freshlyFetched)
    {
        lock (_lock)
        {
            var stored = Load();
            var existingIds = stored.Select(a => a.Id).ToHashSet();

            foreach (var article in freshlyFetched)
            {
                if (!existingIds.Contains(article.Id))
                {
                    stored.Add(article);
                    existingIds.Add(article.Id);
                }
                //if it already exists, the stored copy is left untouched on purpose -
                //FetchedAt should reflect when we first saw it, not when we saw it again
            }

            var cutoff = DateTime.UtcNow.AddDays(-RetentionDays);
            stored = stored.Where(a => a.FetchedAt >= cutoff).ToList();

            Save(stored);

            return stored.Where(a => a.FeedId == feedId).ToList();
        }
    }

    public void RemoveForFeed(Guid feedId)
    {
        lock (_lock)
        {
            var stored = Load();
            stored.RemoveAll(a => a.FeedId == feedId);
            Save(stored);
        }
    }
}
