using RSS_Reader.Models;

namespace RSS_Reader.Storage;

public interface IArticleStore
{
    List<Article> Load();

    // Merges freshly-fetched articles for one feed into the persisted store, keeping any older
    // articles that are still within the retention window even if the live feed no longer serves
    // them, then prunes anything older than retention. Returns this feed's articles after merging.
    List<Article> MergeAndPersist(Guid feedId, List<Article> freshlyFetched);

    void RemoveForFeed(Guid feedId);
}
