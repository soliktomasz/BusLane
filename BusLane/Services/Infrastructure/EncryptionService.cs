namespace BusLane.Services.Infrastructure;

using System.Security.Cryptography;
using System.Text;
using Serilog;

/// <summary>
/// Provides AES-256 encryption for sensitive data with a persisted per-user key.
/// The encryption uses:
/// - AES-256-CBC for encryption
/// - PBKDF2 for key derivation
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
    private readonly string _keyPath;
    private readonly object _keyPersistenceLock = new();
    private bool _isMasterKeyPersisted;
    
    public EncryptionService()
        : this(AppPaths.EncryptionKey, GetLegacyEntropy())
    {
    }

    internal EncryptionService(string keyPath, string legacyEntropy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(legacyEntropy);

        _keyPath = keyPath;
        if (File.Exists(keyPath))
        {
            _masterKey = LoadPersistedMasterKey(keyPath);
            _isMasterKeyPersisted = true;
        }
        else
        {
            _masterKey = DeriveMasterKey(legacyEntropy);
        }
    }

    private static byte[] LoadPersistedMasterKey(string keyPath)
    {
        try
        {
            var persistedKey = Convert.FromBase64String(File.ReadAllText(keyPath));
            if (persistedKey.Length != KeySize / 8)
            {
                throw new CryptographicException("Persisted encryption key has an invalid length.");
            }

            return persistedKey;
        }
        catch (Exception ex) when (ex is FormatException or IOException or UnauthorizedAccessException)
        {
            throw new CryptographicException("Failed to load the persisted encryption key.", ex);
        }
    }

    private void PersistMasterKey()
    {
        if (_isMasterKeyPersisted)
            return;

        lock (_keyPersistenceLock)
        {
            if (_isMasterKeyPersisted)
                return;

            if (File.Exists(_keyPath))
            {
                var persistedKey = LoadPersistedMasterKey(_keyPath);
                if (!CryptographicOperations.FixedTimeEquals(persistedKey, _masterKey))
                {
                    throw new CryptographicException("Persisted encryption key does not match the active key.");
                }
            }
            else
            {
                AppPaths.CreateSecureFile(_keyPath, Convert.ToBase64String(_masterKey));
            }

            _isMasterKeyPersisted = true;
        }
    }

    private static string GetLegacyEntropy()
    {
        var entropyBuilder = new StringBuilder();
        entropyBuilder.Append(Environment.MachineName);
        entropyBuilder.Append(Environment.UserName);
        entropyBuilder.Append("BusLane-v1-SecureStorage");
        entropyBuilder.Append(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        return entropyBuilder.ToString();
    }

    private static byte[] DeriveMasterKey(string legacyEntropy)
    {
        var entropyBytes = Encoding.UTF8.GetBytes(legacyEntropy);
        var fixedSalt = "BusLane-Master-Key-Salt-2025"u8.ToArray();

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
            PersistMasterKey();
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
            Log.Error(ex, "Encryption failed");
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
            var decryptedText = Encoding.UTF8.GetString(decryptedBytes);

            try
            {
                PersistMasterKey();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Decryption succeeded, but the master key could not be persisted");
            }

            return decryptedText;
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Decryption failed - data may be corrupted or encrypted with different key");
            return null;
        }
    }
    
    public bool IsEncrypted(string text)
    {
        return !string.IsNullOrEmpty(text) && text.StartsWith(EncryptionPrefix);
    }
}
