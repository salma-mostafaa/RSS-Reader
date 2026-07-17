using RSS_Reader.Models;

namespace RSS_Reader.Storage;

public interface ISavedStore
{
    List<SavedArticle> LoadAll(Guid userId);
    SavedArticle? AddIfNotPresent(Guid userId, Article article);
    bool Remove(Guid userId, Guid savedItemId);
}