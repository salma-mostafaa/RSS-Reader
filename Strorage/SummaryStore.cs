using System.Text.Json;
using RSS_Reader.Models;

namespace RSS_Reader.Storage;

public class SummaryStore : ISummaryStore
{
    private const string GlobalFilePath = "summary.json";

    private static string GetUserFilePath(string userId) => $"summary_{userId}.json";

    public AiSummary? Load(string? userId)
    {
        var path = userId != null ? GetUserFilePath(userId) : GlobalFilePath;
        if (!File.Exists(path)) return null;
        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<AiSummary>(json);
        }
        catch
        {
            return null;
        }
    }

    public void Save(AiSummary summary, string? userId)
    {
        var path = userId != null ? GetUserFilePath(userId) : GlobalFilePath;
        var json = JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }
}
