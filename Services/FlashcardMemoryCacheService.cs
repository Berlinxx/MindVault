using mindvault.Data;
using mindvault.Services;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace mindvault.Services;

public class FlashcardMemoryCacheService
{
    readonly DatabaseService _db;
    readonly SemaphoreSlim _preloadGate = new(1, 1);
    readonly ConcurrentDictionary<int, List<Flashcard>> _cache = new();
    volatile bool _isPreloading;
    volatile bool _isPreloaded;

    public bool IsPreloaded => _isPreloaded;

    public FlashcardMemoryCacheService(DatabaseService db) => _db = db;

    public async Task PreloadAllAsync()
    {
        await _preloadGate.WaitAsync();
        try
        {
            if (_isPreloading || _isPreloaded) return;
            _isPreloading = true;
        }
        finally { _preloadGate.Release(); }

        try
        {
            var reviewers = await _db.GetReviewersAsync().ConfigureAwait(false);
            var ids = reviewers.Select(r => r.Id).ToList();
            var concurrency = new SemaphoreSlim(4);
            var tasks = new List<Task>();
            foreach (var id in ids)
            {
                await concurrency.WaitAsync().ConfigureAwait(false);
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        var cards = await _db.GetFlashcardsAsync(id).ConfigureAwait(false);
                        _cache[id] = cards.Select(CloneLite).ToList();
                    }
                    finally { concurrency.Release(); }
                }));
            }
            await Task.WhenAll(tasks).ConfigureAwait(false);
            _isPreloaded = true;
        }
        finally { _isPreloading = false; }
    }

    public Task<bool> HasDeckAsync(int reviewerId)
        => Task.FromResult(_cache.ContainsKey(reviewerId));

    public async Task<List<Flashcard>> GetCardsAsync(int reviewerId)
    {
        if (_cache.TryGetValue(reviewerId, out var ready)) return ready;
        var cards = await _db.GetFlashcardsAsync(reviewerId).ConfigureAwait(false);
        var clone = cards.Select(CloneLite).ToList();
        _cache[reviewerId] = clone;
        return clone;
    }

    public void SetCards(int reviewerId, IEnumerable<Flashcard> cards)
        => _cache[reviewerId] = cards.Select(CloneLite).ToList();

    static Flashcard CloneLite(Flashcard c) => new()
    {
        Id = c.Id,
        ReviewerId = c.ReviewerId,
        Question = c.Question,
        Answer = c.Answer,
        QuestionImagePath = c.QuestionImagePath,
        AnswerImagePath = c.AnswerImagePath,
        Learned = c.Learned,
        Order = c.Order
    };
}
