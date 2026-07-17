using System.Collections.Concurrent;

namespace RSS_Reader.Services;

public class AiRateTracker
{
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(20);
    private const int UserLimit = 10;
    private const int GuestLimit = 5;

    private readonly ConcurrentDictionary<string, (int Count, DateTime ResetAt)> _usage = new();

    public (int remaining, int limit) GetStatus(string userId, bool isAuthenticated)
    {
        var limit = isAuthenticated ? UserLimit : GuestLimit;
        var now = DateTime.UtcNow;
        if (!_usage.TryGetValue(userId, out var entry))
            return (limit, limit);

        if (now >= entry.ResetAt)
            return (limit, limit);

        var remaining = limit - entry.Count;
        return (remaining > 0 ? remaining : 0, limit);
    }

    public (int remaining, int limit) RecordAndGetStatus(string userId, bool isAuthenticated)
    {
        var limit = isAuthenticated ? UserLimit : GuestLimit;
        var now = DateTime.UtcNow;
        var entry = _usage.GetOrAdd(userId, _ => (0, now.Add(Window)));

        if (now >= entry.ResetAt)
            entry = (0, now.Add(Window));

        var newCount = entry.Count + 1;
        if (newCount > limit)
        {
            _usage[userId] = entry;
            return (0, limit);
        }

        _usage[userId] = (newCount, entry.ResetAt);
        return (limit - newCount, limit);
    }
}
