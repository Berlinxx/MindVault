using mindvault.Data;

namespace mindvault.Services;

public class GlobalDeckPreloadService
{
    private readonly DatabaseService _db;
    private volatile bool _isLoaded;
    private readonly SemaphoreSlim _gate = new(1,1);

    public Dictionary<int, List<Flashcard>> Decks { get; private set; } = new();

    public GlobalDeckPreloadService(DatabaseService db)
    {
        _db = db;
    }

    public bool IsLoaded => _isLoaded;

    public async Task PreloadAllAsync(bool forceReload = false)
    {
        await _gate.WaitAsync();
        try
        {
            if (_isLoaded && !forceReload) return;
            Decks = new Dictionary<int, List<Flashcard>>();
            var reviewers = await _db.GetReviewersAsync().ConfigureAwait(false);
            foreach (var r in reviewers)
            {
                var cards = await _db.GetFlashcardsAsync(r.Id).ConfigureAwait(false);
                // Store as-is (text + metadata only; images are referenced by path)
                Decks[r.Id] = cards.Select(c => new Flashcard
                {
                    Id = c.Id,
                    ReviewerId = c.ReviewerId,
                    Question = c.Question,
                    Answer = c.Answer,
                    QuestionImagePath = c.QuestionImagePath,
                    AnswerImagePath = c.AnswerImagePath,
                    Learned = c.Learned,
                    Order = c.Order
                }).ToList();
            }
            _isLoaded = true;
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Clear()
    {
        Decks.Clear();
        _isLoaded = false;
    }
}
