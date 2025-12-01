using mindvault.Data;
using SQLite;
using System.Diagnostics;

namespace mindvault.Services;

/// <summary>
/// Handles migration from unencrypted SQLite database to encrypted SQLCipher database.
/// This service runs automatically on app startup to detect and migrate legacy databases.
/// </summary>
public class DatabaseMigrationService
{
    private readonly string _dbPath;
    private readonly string _backupPath;

    public DatabaseMigrationService()
    {
        _dbPath = Path.Combine(FileSystem.AppDataDirectory, "mindvault.db3");
        _backupPath = Path.Combine(FileSystem.AppDataDirectory, "mindvault_backup_unencrypted.db3");
    }

    /// <summary>
    /// Checks if the database needs migration from unencrypted to encrypted.
    /// </summary>
    public async Task<bool> NeedsMigrationAsync()
    {
        try
        {
            // Check if database file exists
            if (!File.Exists(_dbPath))
            {
                Debug.WriteLine("[Migration] No existing database found, no migration needed");
                return false;
            }

            // Check if backup already exists (migration was already done)
            if (File.Exists(_backupPath))
            {
                Debug.WriteLine("[Migration] Backup exists, migration already completed");
                return false;
            }

            // Try to open database without encryption key
            // If this succeeds, database is unencrypted and needs migration
            try
            {
                var testConn = new SQLiteAsyncConnection(_dbPath);
                var tableInfo = await testConn.GetTableInfoAsync("Reviewer");
                
                // If we got here, database is unencrypted
                Debug.WriteLine("[Migration] Unencrypted database detected, migration required");
                return true;
            }
            catch (SQLiteException ex)
            {
                // Database is encrypted or corrupted
                Debug.WriteLine($"[Migration] Database already encrypted or inaccessible: {ex.Message}");
                return false;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Migration] Error checking migration status: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Migrates database from unencrypted to encrypted format.
    /// Creates a backup of the original unencrypted database.
    /// </summary>
    public async Task<(bool Success, string Message)> MigrateToEncryptedAsync(string encryptionKey)
    {
        try
        {
            Debug.WriteLine("[Migration] Starting database migration to encrypted format...");

            // 1. Create backup of unencrypted database
            try
            {
                File.Copy(_dbPath, _backupPath, overwrite: true);
                Debug.WriteLine($"[Migration] Backup created: {_backupPath}");
            }
            catch (Exception ex)
            {
                return (false, $"Failed to create backup: {ex.Message}");
            }

            // 2. Read all data from unencrypted database
            Debug.WriteLine("[Migration] Reading data from unencrypted database...");
            DatabaseService? oldDb = null;
            List<Reviewer> reviewers;
            Dictionary<int, List<Flashcard>> flashcardsByReviewer = new();

            try
            {
                oldDb = new DatabaseService(_dbPath, encryptionKey: null);
                await oldDb.InitializeAsync();
                
                reviewers = await oldDb.GetReviewersAsync();
                Debug.WriteLine($"[Migration] Found {reviewers.Count} reviewers");

                foreach (var reviewer in reviewers)
                {
                    var cards = await oldDb.GetFlashcardsAsync(reviewer.Id);
                    flashcardsByReviewer[reviewer.Id] = cards;
                    Debug.WriteLine($"[Migration] Reviewer '{reviewer.Title}': {cards.Count} cards");
                }
            }
            catch (Exception ex)
            {
                return (false, $"Failed to read old database: {ex.Message}");
            }

            // 3. Delete old unencrypted database
            try
            {
                // Give it a moment to release file handles
                await Task.Delay(100);
                File.Delete(_dbPath);
                Debug.WriteLine("[Migration] Old unencrypted database deleted");
            }
            catch (Exception ex)
            {
                return (false, $"Failed to delete old database: {ex.Message}");
            }

            // 4. Create new encrypted database
            Debug.WriteLine("[Migration] Creating new encrypted database...");
            DatabaseService? newDb = null;
            try
            {
                newDb = new DatabaseService(_dbPath, encryptionKey);
                await newDb.InitializeAsync();
                Debug.WriteLine("[Migration] New encrypted database created");
            }
            catch (Exception ex)
            {
                // Restore backup if new database creation fails
                try
                {
                    File.Copy(_backupPath, _dbPath, overwrite: true);
                    Debug.WriteLine("[Migration] Restored backup after failure");
                }
                catch { }
                
                return (false, $"Failed to create encrypted database: {ex.Message}");
            }

            // 5. Write data to encrypted database
            Debug.WriteLine("[Migration] Writing data to encrypted database...");
            try
            {
                // Map old IDs to new IDs
                Dictionary<int, int> reviewerIdMap = new();

                foreach (var oldReviewer in reviewers)
                {
                    var newReviewer = new Reviewer
                    {
                        Title = oldReviewer.Title,
                        CreatedUtc = oldReviewer.CreatedUtc
                    };
                    
                    await newDb.AddReviewerAsync(newReviewer);
                    reviewerIdMap[oldReviewer.Id] = newReviewer.Id;
                    
                    Debug.WriteLine($"[Migration] Migrated reviewer '{newReviewer.Title}' (old ID: {oldReviewer.Id}, new ID: {newReviewer.Id})");
                }

                int totalCards = 0;
                foreach (var oldReviewerId in flashcardsByReviewer.Keys)
                {
                    if (!reviewerIdMap.TryGetValue(oldReviewerId, out var newReviewerId))
                        continue;

                    var cards = flashcardsByReviewer[oldReviewerId];
                    foreach (var oldCard in cards)
                    {
                        var newCard = new Flashcard
                        {
                            ReviewerId = newReviewerId,
                            Question = oldCard.Question,
                            Answer = oldCard.Answer,
                            QuestionImagePath = oldCard.QuestionImagePath,
                            AnswerImagePath = oldCard.AnswerImagePath,
                            Learned = oldCard.Learned,
                            Order = oldCard.Order
                        };
                        
                        await newDb.AddFlashcardAsync(newCard);
                        totalCards++;
                    }
                }

                Debug.WriteLine($"[Migration] Migrated {totalCards} flashcards");
            }
            catch (Exception ex)
            {
                // Restore backup if data migration fails
                try
                {
                    File.Delete(_dbPath);
                    File.Copy(_backupPath, _dbPath, overwrite: true);
                    Debug.WriteLine("[Migration] Restored backup after data migration failure");
                }
                catch { }
                
                return (false, $"Failed to migrate data: {ex.Message}");
            }

            // 6. Verify migration
            try
            {
                var verifyReviewers = await newDb.GetReviewersAsync();
                int verifyCards = 0;
                foreach (var r in verifyReviewers)
                {
                    var cards = await newDb.GetFlashcardsAsync(r.Id);
                    verifyCards += cards.Count;
                }

                Debug.WriteLine($"[Migration] Verification: {verifyReviewers.Count} reviewers, {verifyCards} cards");

                if (verifyReviewers.Count != reviewers.Count)
                {
                    return (false, "Verification failed: Reviewer count mismatch");
                }

                var expectedCards = flashcardsByReviewer.Values.Sum(list => list.Count);
                if (verifyCards != expectedCards)
                {
                    return (false, $"Verification failed: Card count mismatch (expected {expectedCards}, got {verifyCards})");
                }
            }
            catch (Exception ex)
            {
                return (false, $"Verification failed: {ex.Message}");
            }

            Debug.WriteLine("[Migration] Migration completed successfully!");
            return (true, $"Successfully migrated {reviewers.Count} reviewers and {flashcardsByReviewer.Values.Sum(list => list.Count)} flashcards");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Migration] Unexpected error: {ex}");
            return (false, $"Unexpected error: {ex.Message}");
        }
    }

    /// <summary>
    /// Restores the unencrypted backup database.
    /// WARNING: This will replace the encrypted database with the unencrypted backup!
    /// </summary>
    public bool RestoreBackup()
    {
        try
        {
            if (!File.Exists(_backupPath))
            {
                Debug.WriteLine("[Migration] No backup found to restore");
                return false;
            }

            if (File.Exists(_dbPath))
            {
                File.Delete(_dbPath);
            }

            File.Copy(_backupPath, _dbPath, overwrite: true);
            Debug.WriteLine("[Migration] Backup restored successfully");
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Migration] Failed to restore backup: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Deletes the unencrypted backup after confirming migration was successful.
    /// </summary>
    public bool DeleteBackup()
    {
        try
        {
            if (!File.Exists(_backupPath))
            {
                return true;
            }

            File.Delete(_backupPath);
            Debug.WriteLine("[Migration] Backup deleted successfully");
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Migration] Failed to delete backup: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Gets the size of the backup file for display purposes.
    /// </summary>
    public long GetBackupSizeBytes()
    {
        try
        {
            if (!File.Exists(_backupPath))
                return 0;

            return new FileInfo(_backupPath).Length;
        }
        catch
        {
            return 0;
        }
    }
}
