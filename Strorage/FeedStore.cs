using System.Text.Json;
using RSS_Reader.Models;

namespace RSS_Reader.Storage;

public class FeedStore : IFeedStore
{
    private const string SharedFilePath = "feeds.json";

    private static string GetUserFilePath(string userId) => $"feeds_{userId}.json";

    public List<Feed> Load(string? userId)
    {
        if (userId == null)
            return LoadSharedFeeds();

        return LoadUserFeeds(userId);
    }

    public void SaveUserFeeds(List<Feed> feeds, string userId)
    {
        var userFeeds = feeds.Where(f => f.UserId == userId).ToList();

        var json = JsonSerializer.Serialize(
            userFeeds,
            new JsonSerializerOptions { WriteIndented = true });

        File.WriteAllText(GetUserFilePath(userId), json);
    }

    public void SaveSharedFeeds(List<Feed> feeds)
    {
        var shared = feeds.Where(f => string.IsNullOrEmpty(f.UserId)).ToList();

        var json = JsonSerializer.Serialize(
            shared,
            new JsonSerializerOptions { WriteIndented = true });

        File.WriteAllText(SharedFilePath, json);
    }

    private static List<Feed> LoadSharedFeeds()
    {
        if (!File.Exists(SharedFilePath)) return new List<Feed>();
        try
        {
            var json = File.ReadAllText(SharedFilePath);
            return JsonSerializer.Deserialize<List<Feed>>(json) ?? new List<Feed>();
        }
        catch
        {
            return new List<Feed>();
        }
    }

    private static List<Feed> LoadUserFeeds(string userId)
    {
        var path = GetUserFilePath(userId);
        if (!File.Exists(path))
            return SeedDefaultFeed(userId);

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<List<Feed>>(json) ?? new List<Feed>();
        }
        catch
        {
            return new List<Feed>();
        }
    }

    private static List<Feed> SeedDefaultFeed(string userId)
    {
        var feeds = new List<Feed>
        {
            new Feed
            {
                Id = Guid.NewGuid(),
                Url = "https://feeds.bbci.co.uk/news/rss.xml",
                Title = "BBC News",
                UserId = userId
            }
        };

        var json = JsonSerializer.Serialize(feeds, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(GetUserFilePath(userId), json);

        return feeds;
    }
}
