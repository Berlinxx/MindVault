using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace mindvault.Services;

/// <summary>
/// Tracks failed password attempts and enforces cooldown periods to prevent brute-force attacks.
/// Uses exponential backoff: 30s after 3 attempts, 60s after 5, 5min after 8.
/// Identifies files by content hash to prevent bypass via renaming.
/// </summary>
public class PasswordAttemptService
{
    // Track attempts per file (using content hash as key)
    private readonly Dictionary<string, AttemptInfo> _attempts = new();
    
    // Lockout configuration
    private const int FIRST_LOCKOUT_ATTEMPTS = 3;
    private const int SECOND_LOCKOUT_ATTEMPTS = 5;
    private const int FINAL_LOCKOUT_ATTEMPTS = 8;
    
    private static readonly TimeSpan FIRST_LOCKOUT_DURATION = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan SECOND_LOCKOUT_DURATION = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan FINAL_LOCKOUT_DURATION = TimeSpan.FromMinutes(5);
    
    private class AttemptInfo
    {
        public int FailedAttempts { get; set; }
        public DateTime? LockoutUntil { get; set; }
        public DateTime LastAttempt { get; set; }
    }
    
    /// <summary>
    /// Generates a unique identifier for a file based on its content.
    /// This prevents bypass via renaming the file.
    /// </summary>
    /// <param name="fileContent">The content of the file</param>
    /// <returns>A hash string that uniquely identifies this file's content</returns>
    public static string GenerateFileIdentifier(string fileContent)
    {
        if (string.IsNullOrEmpty(fileContent))
            return "empty_file";
        
        // Use SHA256 hash of the content - even if user renames file, hash stays the same
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(fileContent);
        var hashBytes = sha256.ComputeHash(bytes);
        
        // Return first 16 chars of hex string (64 bits of entropy - plenty for this use case)
        return Convert.ToHexString(hashBytes)[..16];
    }
    
    /// <summary>
    /// Checks if the user is currently locked out from password attempts.
    /// </summary>
    /// <param name="fileIdentifier">Unique identifier for the file (e.g., file name or hash)</param>
    /// <returns>Tuple of (isLockedOut, remainingSeconds)</returns>
    public (bool IsLockedOut, int RemainingSeconds) CheckLockout(string fileIdentifier)
    {
        CleanupOldEntries();
        
        if (!_attempts.TryGetValue(fileIdentifier, out var info))
        {
            return (false, 0);
        }
        
        if (info.LockoutUntil.HasValue && DateTime.UtcNow < info.LockoutUntil.Value)
        {
            var remaining = (int)(info.LockoutUntil.Value - DateTime.UtcNow).TotalSeconds;
            return (true, Math.Max(1, remaining));
        }
        
        return (false, 0);
    }
    
    /// <summary>
    /// Records a failed password attempt and returns lockout status.
    /// </summary>
    /// <param name="fileIdentifier">Unique identifier for the file</param>
    /// <returns>Tuple of (isNowLockedOut, lockoutSeconds, totalAttempts)</returns>
    public (bool IsNowLockedOut, int LockoutSeconds, int TotalAttempts) RecordFailedAttempt(string fileIdentifier)
    {
        if (!_attempts.TryGetValue(fileIdentifier, out var info))
        {
            info = new AttemptInfo();
            _attempts[fileIdentifier] = info;
        }
        
        // If lockout expired, don't reset attempts (they accumulate)
        info.FailedAttempts++;
        info.LastAttempt = DateTime.UtcNow;
        
        // Determine if lockout should be applied
        TimeSpan? lockoutDuration = null;
        
        if (info.FailedAttempts >= FINAL_LOCKOUT_ATTEMPTS)
        {
            lockoutDuration = FINAL_LOCKOUT_DURATION;
        }
        else if (info.FailedAttempts >= SECOND_LOCKOUT_ATTEMPTS)
        {
            lockoutDuration = SECOND_LOCKOUT_DURATION;
        }
        else if (info.FailedAttempts >= FIRST_LOCKOUT_ATTEMPTS)
        {
            lockoutDuration = FIRST_LOCKOUT_DURATION;
        }
        
        if (lockoutDuration.HasValue)
        {
            info.LockoutUntil = DateTime.UtcNow + lockoutDuration.Value;
            return (true, (int)lockoutDuration.Value.TotalSeconds, info.FailedAttempts);
        }
        
        return (false, 0, info.FailedAttempts);
    }
    
    /// <summary>
    /// Records a successful password attempt and clears the attempt history for this file.
    /// </summary>
    /// <param name="fileIdentifier">Unique identifier for the file</param>
    public void RecordSuccessfulAttempt(string fileIdentifier)
    {
        _attempts.Remove(fileIdentifier);
    }
    
    /// <summary>
    /// Gets the number of remaining attempts before lockout.
    /// </summary>
    /// <param name="fileIdentifier">Unique identifier for the file</param>
    /// <returns>Number of attempts remaining before next lockout tier</returns>
    public int GetRemainingAttempts(string fileIdentifier)
    {
        if (!_attempts.TryGetValue(fileIdentifier, out var info))
        {
            return FIRST_LOCKOUT_ATTEMPTS;
        }
        
        if (info.FailedAttempts < FIRST_LOCKOUT_ATTEMPTS)
        {
            return FIRST_LOCKOUT_ATTEMPTS - info.FailedAttempts;
        }
        else if (info.FailedAttempts < SECOND_LOCKOUT_ATTEMPTS)
        {
            return SECOND_LOCKOUT_ATTEMPTS - info.FailedAttempts;
        }
        else if (info.FailedAttempts < FINAL_LOCKOUT_ATTEMPTS)
        {
            return FINAL_LOCKOUT_ATTEMPTS - info.FailedAttempts;
        }
        
        return 0; // In final lockout tier
    }
    
    /// <summary>
    /// Cleans up attempt entries older than 1 hour with no active lockout.
    /// </summary>
    private void CleanupOldEntries()
    {
        var expiredKeys = new List<string>();
        var cutoff = DateTime.UtcNow.AddHours(-1);
        
        foreach (var kvp in _attempts)
        {
            // Remove entries with no active lockout that are old
            if (!kvp.Value.LockoutUntil.HasValue || kvp.Value.LockoutUntil.Value < DateTime.UtcNow)
            {
                if (kvp.Value.LastAttempt < cutoff)
                {
                    expiredKeys.Add(kvp.Key);
                }
            }
        }
        
        foreach (var key in expiredKeys)
        {
            _attempts.Remove(key);
        }
    }
    
    /// <summary>
    /// Formats a user-friendly message about the lockout status.
    /// </summary>
    public static string FormatLockoutMessage(int seconds)
    {
        if (seconds >= 60)
        {
            var minutes = seconds / 60;
            var remainingSecs = seconds % 60;
            if (remainingSecs > 0)
                return $"{minutes} minute{(minutes > 1 ? "s" : "")} and {remainingSecs} second{(remainingSecs > 1 ? "s" : "")}";
            return $"{minutes} minute{(minutes > 1 ? "s" : "")}";
        }
        return $"{seconds} second{(seconds > 1 ? "s" : "")}";
    }
}
