using mindvault.Data;
using System.Collections.Concurrent;

namespace mindvault.Services;

public class DeckMemoryCacheService
{
    private readonly ConcurrentDictionary<int, (List<Flashcard> Cards, DateTime LastAccess)> _cache = new();
    private readonly TimeSpan _ttl = TimeSpan.FromMinutes(10);

    public bool TryGet(int reviewerId, out List<Flashcard> cards)
    {
        if (_cache.TryGetValue(reviewerId, out var entry))
        {
            _cache[reviewerId] = (entry.Cards, DateTime.UtcNow);
            cards = entry.Cards;
            return true;
        }
        cards = null!;
        return false;
    }

    public void Set(int reviewerId, List<Flashcard> cards)
    {
        _cache[reviewerId] = (cards, DateTime.UtcNow);
    }

    public void ClearOldCache()
    {
        var now = DateTime.UtcNow;
        foreach (var kvp in _cache.ToArray())
        {
            if (now - kvp.Value.LastAccess > _ttl)
                _cache.TryRemove(kvp.Key, out _);
        }
    }

    public void ClearAll() => _cache.Clear();
}
