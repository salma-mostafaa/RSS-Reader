using RSS_Reader.Models;

namespace RSS_Reader.Storage;

public interface IArticleSummaryStore
{
    ArticleSummary? Get(Guid articleId);
    void Save(ArticleSummary summary);
}
