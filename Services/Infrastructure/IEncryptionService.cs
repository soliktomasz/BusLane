namespace BusLane.Services.Infrastructure;

/// <summary>
/// Service for encrypting and decrypting sensitive data.
/// </summary>
public interface IEncryptionService
{
    /// <summary>
    /// Encrypts a plain text string.
    /// </summary>
    /// <param name="plainText">The text to encrypt.</param>
    /// <returns>Base64-encoded encrypted data.</returns>
    string Encrypt(string plainText);
    
    /// <summary>
    /// Decrypts an encrypted string.
    /// </summary>
    /// <param name="encryptedText">Base64-encoded encrypted data.</param>
    /// <returns>The decrypted plain text, or null if decryption fails.</returns>
    string? Decrypt(string encryptedText);
    
    /// <summary>
    /// Checks if a string appears to be encrypted (has encryption prefix).
    /// </summary>
    /// <param name="text">The text to check.</param>
    /// <returns>True if the text appears to be encrypted.</returns>
    bool IsEncrypted(string text);
}

