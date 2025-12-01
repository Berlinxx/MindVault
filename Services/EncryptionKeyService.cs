using System.Security.Cryptography;
using System.Text;

namespace mindvault.Services;

/// <summary>
/// Manages encryption keys for database security.
/// Keys are stored in platform-specific secure storage.
/// </summary>
public class EncryptionKeyService
{
    private const string KeyAlias = "mindvault_db_key";
    private const int KeySizeBytes = 32; // 256-bit key

    /// <summary>
    /// Gets or generates the database encryption key.
    /// The key is securely stored using platform-specific secure storage.
    /// </summary>
    public async Task<string> GetOrCreateKeyAsync()
    {
        try
        {
            // Try to retrieve existing key
            var existingKey = await SecureStorage.GetAsync(KeyAlias);
            if (!string.IsNullOrEmpty(existingKey))
            {
                return existingKey;
            }

            // Generate new key if none exists
            var newKey = GenerateKey();
            await SecureStorage.SetAsync(KeyAlias, newKey);
            return newKey;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[EncryptionKeyService] Error: {ex.Message}");
            // Fallback to a deterministic key derived from device ID
            // This is less secure but ensures the app can still function
            return GenerateFallbackKey();
        }
    }

    /// <summary>
    /// Generates a cryptographically secure random key.
    /// </summary>
    private string GenerateKey()
    {
        using var rng = RandomNumberGenerator.Create();
        var keyBytes = new byte[KeySizeBytes];
        rng.GetBytes(keyBytes);
        return Convert.ToBase64String(keyBytes);
    }

    /// <summary>
    /// Generates a fallback key based on device identifier.
    /// Used when SecureStorage fails.
    /// </summary>
    private string GenerateFallbackKey()
    {
        try
        {
            // Use app-specific identifier
            var appId = AppInfo.PackageName ?? "com.mindvault.app";
            var deviceId = Preferences.Get("device_id", string.Empty);
            
            if (string.IsNullOrEmpty(deviceId))
            {
                // Generate and store a device ID
                deviceId = Guid.NewGuid().ToString("N");
                Preferences.Set("device_id", deviceId);
            }

            // Derive key from app ID + device ID
            var input = $"{appId}:{deviceId}";
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
            return Convert.ToBase64String(hash);
        }
        catch
        {
            // Last resort: use a static key (not recommended for production)
            // In production, you'd want to fail gracefully or require user to reinstall
            System.Diagnostics.Debug.WriteLine("[EncryptionKeyService] WARNING: Using static fallback key");
            return "MindVault2024SecureKey_PleaseReinstallIfYouSeeThis";
        }
    }

    /// <summary>
    /// Clears the stored encryption key (for testing or reset purposes).
    /// WARNING: This will make existing encrypted data inaccessible!
    /// </summary>
    public async Task ClearKeyAsync()
    {
        try
        {
            SecureStorage.Remove(KeyAlias);
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[EncryptionKeyService] Clear error: {ex.Message}");
        }
    }

    /// <summary>
    /// Checks if an encryption key exists.
    /// </summary>
    public async Task<bool> KeyExistsAsync()
    {
        try
        {
            var key = await SecureStorage.GetAsync(KeyAlias);
            return !string.IsNullOrEmpty(key);
        }
        catch
        {
            return false;
        }
    }
}
