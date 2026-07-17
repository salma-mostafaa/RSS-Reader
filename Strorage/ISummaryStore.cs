using RSS_Reader.Models;

namespace RSS_Reader.Storage;

public interface ISummaryStore
{
    AiSummary? Load(string? userId);
    void Save(AiSummary summary, string? userId);
}
