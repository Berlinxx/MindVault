using Microsoft.Maui.Storage;
using System.Security.Cryptography;

namespace mindvault.Services;

/// <summary>
/// Manages database encryption keys using platform-specific secure storage.
/// Keys are automatically generated and stored securely without user interaction.
/// </summary>
public static class EncryptionKeyManager
{
    private const string ENCRYPTION_KEY_STORAGE_KEY = "DatabaseEncryptionKey";
    private const int KEY_SIZE_BYTES = 32; // 256-bit key for SQLCipher
    
    /// <summary>
    /// Gets the database encryption key. Generates and stores a new key if none exists.
    /// This is transparent to the user and happens automatically.
    /// </summary>
    /// <returns>Base64-encoded encryption key</returns>
    public static async Task<string> GetOrCreateEncryptionKeyAsync()
    {
        try
        {
            // Try to get existing key from secure storage
            var existingKey = await SecureStorage.GetAsync(ENCRYPTION_KEY_STORAGE_KEY);
            
            if (!string.IsNullOrEmpty(existingKey))
            {
                System.Diagnostics.Debug.WriteLine("[EncryptionKeyManager] Using existing encryption key");
                return existingKey;
            }
            
            // No key exists, generate a new one
            System.Diagnostics.Debug.WriteLine("[EncryptionKeyManager] No encryption key found, generating new key");
            var newKey = GenerateSecureKey();
            
            // Store it securely
            await SecureStorage.SetAsync(ENCRYPTION_KEY_STORAGE_KEY, newKey);
            System.Diagnostics.Debug.WriteLine("[EncryptionKeyManager] New encryption key generated and stored securely");
            
            return newKey;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[EncryptionKeyManager] ERROR: Failed to get/create encryption key: {ex.Message}");
            // In case of error, return null and fall back to unencrypted database
            // This prevents app from crashing but logs the security issue
            return string.Empty;
        }
    }
    
    /// <summary>
    /// Generates a cryptographically secure random encryption key.
    /// Uses the platform's secure random number generator.
    /// </summary>
    /// <returns>Base64-encoded 256-bit key</returns>
    private static string GenerateSecureKey()
    {
        using var rng = RandomNumberGenerator.Create();
        var keyBytes = new byte[KEY_SIZE_BYTES];
        rng.GetBytes(keyBytes);
        
        // Convert to Base64 for storage and use with SQLCipher
        var base64Key = Convert.ToBase64String(keyBytes);
        
        System.Diagnostics.Debug.WriteLine($"[EncryptionKeyManager] Generated {KEY_SIZE_BYTES * 8}-bit encryption key");
        return base64Key;
    }
    
    /// <summary>
    /// Checks if an encryption key exists.
    /// Useful for migration scenarios.
    /// </summary>
    /// <returns>True if encryption key exists, false otherwise</returns>
    public static async Task<bool> HasEncryptionKeyAsync()
    {
        try
        {
            var key = await SecureStorage.GetAsync(ENCRYPTION_KEY_STORAGE_KEY);
            return !string.IsNullOrEmpty(key);
        }
        catch
        {
            return false;
        }
    }
    
    /// <summary>
    /// Deletes the encryption key. 
    /// WARNING: This will make the encrypted database inaccessible!
    /// Only use this when resetting all app data.
    /// </summary>
    public static void DeleteEncryptionKey()
    {
        try
        {
            SecureStorage.Remove(ENCRYPTION_KEY_STORAGE_KEY);
            System.Diagnostics.Debug.WriteLine("[EncryptionKeyManager] Encryption key deleted");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[EncryptionKeyManager] Failed to delete encryption key: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Gets information about the encryption key storage location.
    /// Useful for debugging and developer tools.
    /// </summary>
    /// <returns>Description of where the key is stored</returns>
    public static string GetStorageLocationInfo()
    {
        var platform = DeviceInfo.Platform;
        
        return platform.ToString() switch
        {
            "WinUI" => "Windows DPAPI (Data Protection API) - User account encrypted",
            "Android" => "Android KeyStore - Hardware-backed security",
            "iOS" => "iOS Keychain - Hardware-backed security",
            "MacCatalyst" => "macOS Keychain - Hardware-backed security",
            _ => "Platform-specific secure storage"
        };
    }
    
    /// <summary>
    /// Exports the encryption key for backup purposes.
    /// WARNING: Only use this for legitimate backup scenarios!
    /// The exported key should be stored securely.
    /// </summary>
    /// <returns>The encryption key, or null if none exists</returns>
    public static async Task<string?> ExportKeyForBackupAsync()
    {
        try
        {
            var key = await SecureStorage.GetAsync(ENCRYPTION_KEY_STORAGE_KEY);
            
            if (!string.IsNullOrEmpty(key))
            {
                System.Diagnostics.Debug.WriteLine("[EncryptionKeyManager] WARNING: Encryption key exported!");
                return key;
            }
            
            return null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[EncryptionKeyManager] Failed to export key: {ex.Message}");
            return null;
        }
    }
    
    /// <summary>
    /// Imports an encryption key (for restore from backup).
    /// WARNING: This will overwrite any existing key!
    /// </summary>
    /// <param name="key">The Base64-encoded encryption key to import</param>
    /// <returns>True if successful, false otherwise</returns>
    public static async Task<bool> ImportKeyFromBackupAsync(string key)
    {
        try
        {
            if (string.IsNullOrEmpty(key))
                return false;
            
            // Validate key format (should be Base64)
            try
            {
                var bytes = Convert.FromBase64String(key);
                if (bytes.Length != KEY_SIZE_BYTES)
                {
                    System.Diagnostics.Debug.WriteLine($"[EncryptionKeyManager] Invalid key size: {bytes.Length} bytes (expected {KEY_SIZE_BYTES})");
                    return false;
                }
            }
            catch
            {
                System.Diagnostics.Debug.WriteLine("[EncryptionKeyManager] Invalid key format (not Base64)");
                return false;
            }
            
            // Store the imported key
            await SecureStorage.SetAsync(ENCRYPTION_KEY_STORAGE_KEY, key);
            System.Diagnostics.Debug.WriteLine("[EncryptionKeyManager] Encryption key imported successfully");
            
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[EncryptionKeyManager] Failed to import key: {ex.Message}");
            return false;
        }
    }
}
