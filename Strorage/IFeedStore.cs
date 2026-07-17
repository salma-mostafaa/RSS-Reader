using RSS_Reader.Models;

namespace RSS_Reader.Storage;

public interface IFeedStore
{
    List<Feed> Load(string? userId);
    void SaveUserFeeds(List<Feed> feeds, string userId);
    void SaveSharedFeeds(List<Feed> feeds);
}
