namespace BusLane.Services.Storage;

using System.Security.Cryptography;
using System.Text;
using BusLane.Models;
using BusLane.Services.Infrastructure;
using Serilog;
using static BusLane.Services.Infrastructure.SafeJsonSerializer;

/// <summary>
/// Handles secure export/import of saved connections using passphrase-based encryption.
/// </summary>
public class ConnectionBackupService : IConnectionBackupService
{
    private const string BackupVersion = "1.0";
    private const string EncryptionAlgorithm = "AES-256-GCM";
    private const string KeyDerivationAlgorithm = "PBKDF2-SHA256";
    private const int KeySizeBytes = 32;
    private const int SaltSizeBytes = 16;
    private const int NonceSizeBytes = 12;
    private const int TagSizeBytes = 16;
    private const int KeyDerivationIterations = 210000;
    private const int MaxKeyDerivationIterations = 1000000;

    public Task ExportAsync(IEnumerable<SavedConnection> connections, string filePath, string passphrase)
    {
        ArgumentNullException.ThrowIfNull(connections);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(passphrase);

        var payload = new ConnectionBackupPayload
        {
            ExportedAtUtc = DateTimeOffset.UtcNow,
            Connections = connections
                .Select(c => new ConnectionBackupItem(
                    c.Id,
                    c.Name,
                    c.ConnectionString,
                    c.Type,
                    c.EntityName,
                    c.CreatedAt,
                    c.IsFavorite,
                    c.Environment))
                .ToList()
        };

        var payloadJson = Serialize(payload);
        var encrypted = EncryptPayload(payloadJson, passphrase);

        var backupFile = new ConnectionBackupFile
        {
            Version = BackupVersion,
            Cipher = EncryptionAlgorithm,
            Kdf = KeyDerivationAlgorithm,
            Iterations = KeyDerivationIterations,
            Salt = Convert.ToBase64String(encrypted.Salt),
            Nonce = Convert.ToBase64String(encrypted.Nonce),
            CipherText = Convert.ToBase64String(encrypted.CipherText),
            Tag = Convert.ToBase64String(encrypted.Tag)
        };

        var backupJson = Serialize(backupFile);
        AppPaths.CreateSecureFile(filePath, backupJson);

        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<SavedConnection>> ImportAsync(string filePath, string passphrase)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(passphrase);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("Backup file not found.", filePath);
        }

        var backupJson = await File.ReadAllTextAsync(filePath);
        var backupFile = Deserialize<ConnectionBackupFile>(backupJson);

        if (backupFile == null)
        {
            throw new InvalidDataException("Backup file is empty or invalid.");
        }

        ValidateBackupFile(backupFile);

        var payloadJson = DecryptPayload(backupFile, passphrase);
        if (payloadJson == null)
        {
            throw new CryptographicException("Failed to decrypt backup. Passphrase is invalid or file is corrupted.");
        }

        var payload = Deserialize<ConnectionBackupPayload>(payloadJson);
        if (payload?.Connections == null || payload.Connections.Count == 0)
        {
            return [];
        }

        return payload.Connections
            .Where(IsValidItem)
            .Select(item => new SavedConnection
            {
                Id = string.IsNullOrWhiteSpace(item.Id) ? Guid.NewGuid().ToString() : item.Id,
                Name = item.Name,
                ConnectionString = item.ConnectionString,
                Type = item.Type,
                EntityName = item.EntityName,
                CreatedAt = item.CreatedAt == default ? DateTimeOffset.UtcNow : item.CreatedAt,
                IsFavorite = item.IsFavorite,
                Environment = item.Environment
            })
            .ToList();
    }

    private static EncryptedPayload EncryptPayload(string plainText, string passphrase)
    {
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var salt = RandomNumberGenerator.GetBytes(SaltSizeBytes);
        var nonce = RandomNumberGenerator.GetBytes(NonceSizeBytes);
        var cipherText = new byte[plainBytes.Length];
        var tag = new byte[TagSizeBytes];

        var key = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(passphrase),
            salt,
            KeyDerivationIterations,
            HashAlgorithmName.SHA256,
            KeySizeBytes);

        try
        {
            using var aes = new AesGcm(key, TagSizeBytes);
            aes.Encrypt(nonce, plainBytes, cipherText, tag);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }

        return new EncryptedPayload(salt, nonce, cipherText, tag);
    }

    private static string? DecryptPayload(ConnectionBackupFile backupFile, string passphrase)
    {
        try
        {
            var salt = Convert.FromBase64String(backupFile.Salt);
            var nonce = Convert.FromBase64String(backupFile.Nonce);
            var cipherText = Convert.FromBase64String(backupFile.CipherText);
            var tag = Convert.FromBase64String(backupFile.Tag);

            var plainBytes = new byte[cipherText.Length];
            var key = Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(passphrase),
                salt,
                backupFile.Iterations,
                HashAlgorithmName.SHA256,
                KeySizeBytes);

            try
            {
                using var aes = new AesGcm(key, TagSizeBytes);
                aes.Decrypt(nonce, cipherText, tag, plainBytes);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(key);
            }

            return Encoding.UTF8.GetString(plainBytes);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Failed to decrypt connection backup file");
            return null;
        }
    }

    private static bool IsValidItem(ConnectionBackupItem item)
    {
        return !string.IsNullOrWhiteSpace(item.Name)
            && !string.IsNullOrWhiteSpace(item.ConnectionString);
    }

    private static void ValidateBackupFile(ConnectionBackupFile backupFile)
    {
        if (!string.Equals(backupFile.Version, BackupVersion, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Unsupported backup version '{backupFile.Version}'.");
        }

        if (!string.Equals(backupFile.Cipher, EncryptionAlgorithm, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Unsupported backup encryption algorithm.");
        }

        if (!string.Equals(backupFile.Kdf, KeyDerivationAlgorithm, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Unsupported key derivation algorithm.");
        }

        if (backupFile.Iterations <= 0)
        {
            throw new InvalidDataException("Backup key derivation iterations are invalid.");
        }

        if (backupFile.Iterations > MaxKeyDerivationIterations)
        {
            throw new InvalidDataException("Backup key derivation iterations exceed the supported maximum.");
        }

        if (string.IsNullOrWhiteSpace(backupFile.Salt)
            || string.IsNullOrWhiteSpace(backupFile.Nonce)
            || string.IsNullOrWhiteSpace(backupFile.CipherText)
            || string.IsNullOrWhiteSpace(backupFile.Tag))
        {
            throw new InvalidDataException("Backup file is missing required encrypted fields.");
        }
    }

    private sealed record EncryptedPayload(
        byte[] Salt,
        byte[] Nonce,
        byte[] CipherText,
        byte[] Tag);

    private sealed class ConnectionBackupFile
    {
        public string Version { get; set; } = BackupVersion;
        public string Cipher { get; set; } = EncryptionAlgorithm;
        public string Kdf { get; set; } = KeyDerivationAlgorithm;
        public int Iterations { get; set; } = KeyDerivationIterations;
        public string Salt { get; set; } = string.Empty;
        public string Nonce { get; set; } = string.Empty;
        public string CipherText { get; set; } = string.Empty;
        public string Tag { get; set; } = string.Empty;
    }

    private sealed class ConnectionBackupPayload
    {
        public DateTimeOffset ExportedAtUtc { get; set; } = DateTimeOffset.UtcNow;
        public List<ConnectionBackupItem> Connections { get; set; } = [];
    }

    private sealed record ConnectionBackupItem(
        string Id,
        string Name,
        string ConnectionString,
        ConnectionType Type,
        string? EntityName,
        DateTimeOffset CreatedAt,
        bool IsFavorite,
        ConnectionEnvironment Environment);
}
