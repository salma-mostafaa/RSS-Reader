using RSS_Reader.Models;

namespace RSS_Reader.Storage;

public interface IHistoryStore
{
    List<HistoryEntry> LoadAll(Guid userId);
    List<Guid> LoadViewedArticleIds(Guid userId);
    HistoryEntry LogView(Guid userId, Article article);
    void Clear(Guid userId);
}