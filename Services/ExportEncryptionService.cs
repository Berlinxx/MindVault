using System.Security.Cryptography;
using System.Text;

namespace mindvault.Services;

/// <summary>
/// Provides password-based encryption for export files using AES-256.
/// </summary>
public static class ExportEncryptionService
{
    private const int KeySize = 256; // AES-256
    private const int Iterations = 10000; // PBKDF2 iterations
    private const int SaltSize = 16; // 128-bit salt
    private const int IVSize = 16; // 128-bit IV for AES

    /// <summary>
    /// Encrypts JSON content with a password.
    /// Returns base64-encoded encrypted data with format: ENCRYPTED:[salt]:[iv]:[ciphertext]
    /// </summary>
    public static string Encrypt(string plainText, string password)
    {
        if (string.IsNullOrEmpty(plainText))
            throw new ArgumentException("Plain text cannot be empty", nameof(plainText));
        if (string.IsNullOrEmpty(password))
            throw new ArgumentException("Password cannot be empty", nameof(password));

        // Generate random salt
        byte[] salt = new byte[SaltSize];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(salt);
        }

        // Derive key from password
        using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, Iterations, HashAlgorithmName.SHA256);
        byte[] key = pbkdf2.GetBytes(KeySize / 8);

        // Encrypt
        using var aes = Aes.Create();
        aes.KeySize = KeySize;
        aes.Key = key;
        aes.GenerateIV();
        byte[] iv = aes.IV;

        using var encryptor = aes.CreateEncryptor();
        using var msEncrypt = new MemoryStream();
        using (var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
        using (var swEncrypt = new StreamWriter(csEncrypt))
        {
            swEncrypt.Write(plainText);
        }

        byte[] encrypted = msEncrypt.ToArray();

        // Format: ENCRYPTED:[salt]:[iv]:[ciphertext]
        var result = $"ENCRYPTED:{Convert.ToBase64String(salt)}:{Convert.ToBase64String(iv)}:{Convert.ToBase64String(encrypted)}";
        return result;
    }

    /// <summary>
    /// Decrypts encrypted content with a password.
    /// </summary>
    public static string Decrypt(string encryptedText, string password)
    {
        if (string.IsNullOrEmpty(encryptedText))
            throw new ArgumentException("Encrypted text cannot be empty", nameof(encryptedText));
        if (string.IsNullOrEmpty(password))
            throw new ArgumentException("Password cannot be empty", nameof(password));

        // Parse format: ENCRYPTED:[salt]:[iv]:[ciphertext]
        if (!encryptedText.StartsWith("ENCRYPTED:"))
            throw new ArgumentException("Invalid encrypted format");

        var parts = encryptedText.Substring(10).Split(':');
        if (parts.Length != 3)
            throw new ArgumentException("Invalid encrypted format");

        byte[] salt = Convert.FromBase64String(parts[0]);
        byte[] iv = Convert.FromBase64String(parts[1]);
        byte[] cipherText = Convert.FromBase64String(parts[2]);

        // Derive key from password
        using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, Iterations, HashAlgorithmName.SHA256);
        byte[] key = pbkdf2.GetBytes(KeySize / 8);

        // Decrypt
        using var aes = Aes.Create();
        aes.KeySize = KeySize;
        aes.Key = key;
        aes.IV = iv;

        using var decryptor = aes.CreateDecryptor();
        using var msDecrypt = new MemoryStream(cipherText);
        using var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read);
        using var srDecrypt = new StreamReader(csDecrypt);
        
        return srDecrypt.ReadToEnd();
    }

    /// <summary>
    /// Checks if content is encrypted.
    /// </summary>
    public static bool IsEncrypted(string content)
    {
        return content?.StartsWith("ENCRYPTED:") == true;
    }
}
