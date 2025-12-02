using SQLite;
using mindvault.Data;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace mindvault.Services;

public class DatabaseService
{
    readonly SQLiteAsyncConnection _db;

    // Simple in-memory cache of flashcards per reviewer to speed repeated loads
    readonly ConcurrentDictionary<int, List<Flashcard>> _flashcardCache = new();

    /// <summary>
    /// Creates a new DatabaseService with SQLCipher encryption.
    /// </summary>
    /// <param name="dbPath">Path to the database file</param>
    /// <param name="encryptionKey">Encryption key for SQLCipher (Base64 encoded)</param>
    public DatabaseService(string dbPath, string? encryptionKey = null)
    {
        // SQLCipher connection string with encryption
        if (!string.IsNullOrEmpty(encryptionKey))
        {
            var connectionString = new SQLiteConnectionString(dbPath, 
                SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.FullMutex,
                storeDateTimeAsTicks: true,
                key: encryptionKey);
            _db = new SQLiteAsyncConnection(connectionString);
            Debug.WriteLine("[DatabaseService] Database initialized with SQLCipher encryption");
        }
        else
        {
            // Fallback to unencrypted (not recommended for production)
            _db = new SQLiteAsyncConnection(dbPath);
            Debug.WriteLine("[DatabaseService] WARNING: Database initialized WITHOUT encryption");
        }
    }

    public async Task InitializeAsync()
    {
        try
        {
            await _db.CreateTableAsync<Reviewer>();
            await _db.CreateTableAsync<Flashcard>();
            // Try add new columns when upgrading from older schema
            try { await _db.ExecuteAsync("ALTER TABLE Flashcard ADD COLUMN QuestionImagePath TEXT"); } catch { }
            try { await _db.ExecuteAsync("ALTER TABLE Flashcard ADD COLUMN AnswerImagePath TEXT"); } catch { }
            Debug.WriteLine("[DatabaseService] Database tables created successfully");
        }
        catch (SQLite.SQLiteException ex)
        {
            Debug.WriteLine($"[DatabaseService] CRITICAL: Database initialization failed: {ex.Message}");
            Debug.WriteLine($"[DatabaseService] This usually means encryption key mismatch or corrupted database");
            throw new InvalidOperationException("Database initialization failed. The database may be corrupted or encrypted with a different key.", ex);
        }
    }

    public Task<int> AddReviewerAsync(Reviewer reviewer) => _db.InsertAsync(reviewer);
    public Task<int> AddFlashcardAsync(Flashcard card)
    {
        // Invalidate cache for this reviewer on data change
        _flashcardCache.TryRemove(card.ReviewerId, out _);
        return _db.InsertAsync(card);
    }

    public Task<int> UpdateFlashcardAsync(Flashcard card)
    {
        _flashcardCache.TryRemove(card.ReviewerId, out _);
        return _db.UpdateAsync(card);
    }

    public Task<List<Reviewer>> GetReviewersAsync() => _db.Table<Reviewer>().OrderByDescending(r => r.Id).ToListAsync();
    public Task<List<Flashcard>> GetFlashcardsAsync(int reviewerId) => _db.Table<Flashcard>().Where(c => c.ReviewerId == reviewerId).OrderBy(c => c.Order).ToListAsync();

    // Cached accessors for editor/viewer pages
    public async Task<List<Flashcard>> GetFlashcardsCachedAsync(int reviewerId, bool allowStale = true)
    {
        if (allowStale && _flashcardCache.TryGetValue(reviewerId, out var cached))
        {
            return cached;
        }
        var fresh = await GetFlashcardsAsync(reviewerId).ConfigureAwait(false);
        _flashcardCache[reviewerId] = fresh;
        return fresh;
    }

    public void InvalidateFlashcardsCache(int reviewerId) => _flashcardCache.TryRemove(reviewerId, out _);

    // Aggregated counts to speed up reviewer list loading
    public Task<List<ReviewerStats>> GetReviewerStatsAsync() => _db.QueryAsync<ReviewerStats>(
        "SELECT ReviewerId as ReviewerId, COUNT(*) as Total, SUM(CASE WHEN Learned = 1 THEN 1 ELSE 0 END) as Learned FROM Flashcard GROUP BY ReviewerId");

    public Task<int> DeleteReviewerAsync(Reviewer reviewer) => _db.DeleteAsync(reviewer);
    public async Task<int> DeleteReviewerCascadeAsync(int reviewerId)
    {
        _flashcardCache.TryRemove(reviewerId, out _);
        var cards = await GetFlashcardsAsync(reviewerId).ConfigureAwait(false);
        foreach (var c in cards)
            await _db.DeleteAsync(c).ConfigureAwait(false);
        return await _db.DeleteAsync(new Reviewer { Id = reviewerId }).ConfigureAwait(false);
    }

    public Task<int> DeleteFlashcardsForReviewerAsync(int reviewerId)
    {
        _flashcardCache.TryRemove(reviewerId, out _);
        return _db.ExecuteAsync("DELETE FROM Flashcard WHERE ReviewerId = ?", reviewerId);
    }

    public async Task<int> DeleteFlashcardAsync(int id)
    {
        // Invalidate all caches containing this card (unknown reviewer). Best effort: read reviewer id first.
        var row = await _db.FindAsync<Flashcard>(id).ConfigureAwait(false);
        if (row is not null)
            _flashcardCache.TryRemove(row.ReviewerId, out _);
        return await _db.ExecuteAsync("DELETE FROM Flashcard WHERE Id = ?", id).ConfigureAwait(false);
    }

    public Task<int> UpdateReviewerTitleAsync(int reviewerId, string newTitle)
        => _db.ExecuteAsync("UPDATE Reviewer SET Title = ? WHERE Id = ?", newTitle, reviewerId);

    public Task<int> UpdateFlashcardLearnedAsync(int flashcardId, bool learned)
    {
        return _db.ExecuteAsync("UPDATE Flashcard SET Learned = ? WHERE Id = ?", learned ? 1 : 0, flashcardId);
    }
}

public class ReviewerStats
{
    public int ReviewerId { get; set; }
    public int Total { get; set; }
    public int Learned { get; set; }
}
