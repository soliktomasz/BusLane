namespace BusLane.Services.Infrastructure;

using System.Security.Cryptography;
using System.Text;

/// <summary>
/// Provides AES-256 encryption for sensitive data with a machine-specific key.
/// The encryption uses:
/// - AES-256-CBC for encryption
/// - PBKDF2 for key derivation from machine-specific entropy
/// - Random IV for each encryption operation
/// </summary>
public class EncryptionService : IEncryptionService
{
    private const string EncryptionPrefix = "ENC:";
    private const int KeySize = 256;
    private const int BlockSize = 128;
    private const int SaltSize = 16;
    private const int IvSize = 16;
    private const int Iterations = 100000;
    
    private readonly byte[] _masterKey;
    
    public EncryptionService()
    {
        _masterKey = DeriveMasterKey();
    }
    
    /// <summary>
    /// Derives a machine-specific master key using available entropy sources.
    /// This key is deterministic for the same machine/user but different across machines.
    /// </summary>
    private static byte[] DeriveMasterKey()
    {
        // Combine multiple entropy sources for the master key
        // IMPORTANT: Only use stable values that don't change between runs/debug sessions
        var entropyBuilder = new StringBuilder();
        
        // Machine name
        entropyBuilder.Append(Environment.MachineName);
        
        // User name
        entropyBuilder.Append(Environment.UserName);
        
        // App-specific salt
        entropyBuilder.Append("BusLane-v1-SecureStorage");
        
        // User profile path (different per user)
        entropyBuilder.Append(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        
        // Note: We intentionally do NOT use Environment.OSVersion.VersionString 
        // because it can change between OS updates or debug sessions, causing
        // previously encrypted data to become undecryptable.
        
        var entropy = entropyBuilder.ToString();
        var entropyBytes = Encoding.UTF8.GetBytes(entropy);
        
        // Use a fixed salt for key derivation (app-specific)
        var fixedSalt = "BusLane-Master-Key-Salt-2025"u8.ToArray();
        
        // Derive a 256-bit key using PBKDF2
        return Rfc2898DeriveBytes.Pbkdf2(
            entropyBytes, 
            fixedSalt, 
            Iterations, 
            HashAlgorithmName.SHA256,
            KeySize / 8);
    }
    
    public string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            return plainText;
            
        try
        {
            var plainBytes = Encoding.UTF8.GetBytes(plainText);
            
            // Generate random salt and IV for this encryption
            var salt = RandomNumberGenerator.GetBytes(SaltSize);
            var iv = RandomNumberGenerator.GetBytes(IvSize);
            
            // Derive encryption key from master key + salt
            var key = Rfc2898DeriveBytes.Pbkdf2(
                _masterKey, 
                salt, 
                Iterations, 
                HashAlgorithmName.SHA256,
                KeySize / 8);
            
            // Encrypt using AES-256-CBC
            using var aes = Aes.Create();
            aes.KeySize = KeySize;
            aes.BlockSize = BlockSize;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.Key = key;
            aes.IV = iv;
            
            using var encryptor = aes.CreateEncryptor();
            var encryptedBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
            
            // Combine salt + IV + encrypted data
            var result = new byte[SaltSize + IvSize + encryptedBytes.Length];
            Buffer.BlockCopy(salt, 0, result, 0, SaltSize);
            Buffer.BlockCopy(iv, 0, result, SaltSize, IvSize);
            Buffer.BlockCopy(encryptedBytes, 0, result, SaltSize + IvSize, encryptedBytes.Length);
            
            // Return with prefix to identify encrypted data
            return EncryptionPrefix + Convert.ToBase64String(result);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Encryption failed: {ex.Message}");
            throw new CryptographicException("Failed to encrypt data", ex);
        }
    }
    
    public string? Decrypt(string encryptedText)
    {
        if (string.IsNullOrEmpty(encryptedText))
            return encryptedText;
            
        // If not encrypted, return as-is (for backward compatibility)
        if (!IsEncrypted(encryptedText))
            return encryptedText;
            
        try
        {
            // Remove prefix
            var base64Data = encryptedText.Substring(EncryptionPrefix.Length);
            var encryptedData = Convert.FromBase64String(base64Data);
            
            if (encryptedData.Length < SaltSize + IvSize + 1)
                return null;
            
            // Extract salt, IV, and encrypted bytes
            var salt = new byte[SaltSize];
            var iv = new byte[IvSize];
            var cipherBytes = new byte[encryptedData.Length - SaltSize - IvSize];
            
            Buffer.BlockCopy(encryptedData, 0, salt, 0, SaltSize);
            Buffer.BlockCopy(encryptedData, SaltSize, iv, 0, IvSize);
            Buffer.BlockCopy(encryptedData, SaltSize + IvSize, cipherBytes, 0, cipherBytes.Length);
            
            // Derive the same encryption key
            var key = Rfc2898DeriveBytes.Pbkdf2(
                _masterKey, 
                salt, 
                Iterations, 
                HashAlgorithmName.SHA256,
                KeySize / 8);
            
            // Decrypt using AES-256-CBC
            using var aes = Aes.Create();
            aes.KeySize = KeySize;
            aes.BlockSize = BlockSize;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.Key = key;
            aes.IV = iv;
            
            using var decryptor = aes.CreateDecryptor();
            var decryptedBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
            
            return Encoding.UTF8.GetString(decryptedBytes);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Decryption failed: {ex.Message}");
            return null;
        }
    }
    
    public bool IsEncrypted(string text)
    {
        return !string.IsNullOrEmpty(text) && text.StartsWith(EncryptionPrefix);
    }
}

