namespace BusLane.Services.Security;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BusLane.Models.Security;
using BusLane.Services.Infrastructure;
using Serilog;
using static BusLane.Services.Infrastructure.SafeJsonSerializer;

/// <summary>
/// Persists and verifies the opt-in application lock configuration.
/// </summary>
public sealed class AppLockService : IAppLockService
{
    private const int StorageVersion = 1;
    private const int SecretHashVersion = 1;
    private const int SaltSizeBytes = 16;
    private const int HashSizeBytes = 32;
    private const int Iterations = 210000;
    private const string Kdf = "PBKDF2-SHA256";

    private readonly string _filePath;

    public AppLockService(string? filePath = null)
    {
        _filePath = filePath ?? AppPaths.AppLock;
    }

    public async Task<AppLockSnapshot> GetSnapshotAsync(CancellationToken ct = default)
    {
        var state = await LoadStateAsync(ct);
        return state == null || !state.IsEnabled
            ? AppLockSnapshot.Disabled
            : new AppLockSnapshot(state.IsEnabled, state.BiometricUnlockEnabled);
    }

    public async Task<bool> VerifyPasswordAsync(string password, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            return false;
        }

        var state = await LoadStateAsync(ct);
        return state?.IsEnabled == true && VerifySecret(password, state.PasswordHash, normalizeRecoveryCode: false);
    }

    public async Task<string> EnableAsync(AppLockConfiguration configuration, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(configuration.Password);

        var recoveryCode = GenerateRecoveryCode();
        var state = new StoredAppLockState
        {
            Version = StorageVersion,
            IsEnabled = true,
            BiometricUnlockEnabled = configuration.BiometricUnlockEnabled,
            PasswordHash = CreateSecretHash(configuration.Password, normalizeRecoveryCode: false),
            RecoveryCodeHash = CreateSecretHash(recoveryCode, normalizeRecoveryCode: true)
        };

        await PersistStateAsync(state, ct);
        return recoveryCode;
    }

    public Task DisableAsync(CancellationToken ct = default)
    {
        if (File.Exists(_filePath))
        {
            File.Delete(_filePath);
        }

        return Task.CompletedTask;
    }

    public async Task ChangePasswordAsync(string newPassword, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newPassword);

        var state = await LoadRequiredStateAsync(ct);
        state.PasswordHash = CreateSecretHash(newPassword, normalizeRecoveryCode: false);
        await PersistStateAsync(state, ct);
    }

    public async Task<bool> VerifyRecoveryCodeAsync(string recoveryCode, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(recoveryCode))
        {
            return false;
        }

        var state = await LoadStateAsync(ct);
        return state?.IsEnabled == true && VerifySecret(recoveryCode, state.RecoveryCodeHash, normalizeRecoveryCode: true);
    }

    public async Task<string> RegenerateRecoveryCodeAsync(CancellationToken ct = default)
    {
        var state = await LoadRequiredStateAsync(ct);
        var recoveryCode = GenerateRecoveryCode();
        state.RecoveryCodeHash = CreateSecretHash(recoveryCode, normalizeRecoveryCode: true);
        await PersistStateAsync(state, ct);
        return recoveryCode;
    }

    public async Task UpdateBiometricPreferenceAsync(bool enabled, CancellationToken ct = default)
    {
        var state = await LoadRequiredStateAsync(ct);
        state.BiometricUnlockEnabled = enabled;
        await PersistStateAsync(state, ct);
    }

    private async Task<StoredAppLockState?> LoadStateAsync(CancellationToken ct)
    {
        if (!File.Exists(_filePath))
        {
            return null;
        }

        try
        {
            var json = await File.ReadAllTextAsync(_filePath, ct);
            var state = Deserialize<StoredAppLockState>(json);

            if (!IsValidState(state))
            {
                Log.Warning("App lock state at {Path} is invalid. Treating app lock as disabled.", _filePath);
                return null;
            }

            return state;
        }
        catch (FileNotFoundException ex)
        {
            Log.Warning(ex, "App lock state file at {Path} disappeared during load. Treating app lock as disabled.", _filePath);
            return null;
        }
        catch (JsonException ex)
        {
            Log.Warning(ex, "App lock state at {Path} could not be deserialized. Treating app lock as disabled.", _filePath);
            return null;
        }
        catch (ArgumentException ex) when (string.Equals(ex.ParamName, "json", StringComparison.Ordinal))
        {
            Log.Warning(ex, "App lock state at {Path} is malformed. Treating app lock as disabled.", _filePath);
            return null;
        }
    }

    private async Task<StoredAppLockState> LoadRequiredStateAsync(CancellationToken ct)
    {
        var state = await LoadStateAsync(ct);
        if (state?.IsEnabled != true)
        {
            throw new InvalidOperationException("App lock is not enabled.");
        }

        return state;
    }

    private async Task PersistStateAsync(StoredAppLockState state, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var json = Serialize(state);
        await AppPaths.CreateSecureFileAsync(_filePath, json);
    }

    private static bool IsValidState(StoredAppLockState? state)
    {
        return state != null
            && state.Version == StorageVersion
            && state.PasswordHash != null
            && state.RecoveryCodeHash != null
            && IsValidSecretHash(state.PasswordHash)
            && IsValidSecretHash(state.RecoveryCodeHash);
    }

    private static bool IsValidSecretHash(StoredSecretHash hash)
    {
        return hash.Version == SecretHashVersion
            && hash.Iterations > 0
            && !string.IsNullOrWhiteSpace(hash.Salt)
            && !string.IsNullOrWhiteSpace(hash.Hash)
            && string.Equals(hash.Kdf, Kdf, StringComparison.Ordinal);
    }

    private static StoredSecretHash CreateSecretHash(string secret, bool normalizeRecoveryCode)
    {
        var normalizedSecret = normalizeRecoveryCode ? NormalizeRecoveryCode(secret) : secret;
        var salt = RandomNumberGenerator.GetBytes(SaltSizeBytes);
        var secretBytes = Encoding.UTF8.GetBytes(normalizedSecret);
        var hash = Rfc2898DeriveBytes.Pbkdf2(secretBytes, salt, Iterations, HashAlgorithmName.SHA256, HashSizeBytes);

        return new StoredSecretHash
        {
            Version = SecretHashVersion,
            Kdf = Kdf,
            Iterations = Iterations,
            Salt = Convert.ToBase64String(salt),
            Hash = Convert.ToBase64String(hash)
        };
    }

    private static bool VerifySecret(string secret, StoredSecretHash storedHash, bool normalizeRecoveryCode)
    {
        try
        {
            var normalizedSecret = normalizeRecoveryCode ? NormalizeRecoveryCode(secret) : secret;
            var secretBytes = Encoding.UTF8.GetBytes(normalizedSecret);
            var salt = Convert.FromBase64String(storedHash.Salt);
            var expectedHash = Convert.FromBase64String(storedHash.Hash);
            var actualHash = Rfc2898DeriveBytes.Pbkdf2(secretBytes, salt, storedHash.Iterations, HashAlgorithmName.SHA256, expectedHash.Length);

            return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
        }
        catch
        {
            return false;
        }
    }

    private static string GenerateRecoveryCode()
    {
        var code = Convert.ToHexString(RandomNumberGenerator.GetBytes(8));
        return string.Join("-", Enumerable.Range(0, code.Length / 4).Select(index => code.Substring(index * 4, 4)));
    }

    private static string NormalizeRecoveryCode(string recoveryCode)
    {
        return recoveryCode.Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Trim()
            .ToUpperInvariant();
    }

    private sealed class StoredAppLockState
    {
        public int Version { get; set; } = StorageVersion;
        public bool IsEnabled { get; set; }
        public bool BiometricUnlockEnabled { get; set; }
        public StoredSecretHash PasswordHash { get; set; } = new();
        public StoredSecretHash RecoveryCodeHash { get; set; } = new();
    }

    private sealed class StoredSecretHash
    {
        public int Version { get; set; } = SecretHashVersion;
        public string Kdf { get; set; } = AppLockService.Kdf;
        public int Iterations { get; set; } = AppLockService.Iterations;
        public string Salt { get; set; } = string.Empty;
        public string Hash { get; set; } = string.Empty;
    }
}
