namespace BusLane.Services.Auth;

using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using BusLane.Services.Infrastructure;
using Serilog;

public class AzureAuthService : IAzureAuthService
{
    private TokenCredential? _credential;
    private ArmClient? _armClient;
    private AuthenticationRecord? _authRecord;
    private readonly TokenCachePersistenceOptions _cacheOptions;
    private readonly string _authRecordPath;

    public bool IsAuthenticated { get; private set; }
    public string? UserName { get; private set; }
    public TokenCredential? Credential => _credential;
    public ArmClient? ArmClient => _armClient;

    public event EventHandler<bool>? AuthenticationChanged;

    /// <summary>
    /// Event raised when device code authentication requires user action.
    /// </summary>
    public event EventHandler<DeviceCodeInfo>? DeviceCodeRequired;

    public AzureAuthService()
    {
        // Enable token cache persistence - tokens will be stored securely
        // On macOS: Keychain, On Windows: DPAPI, On Linux: encrypted file
        _cacheOptions = new TokenCachePersistenceOptions
        {
            Name = "BusLane"
        };

        // Path to store the authentication record (identifies the account)
        var configDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "BusLane");
        Directory.CreateDirectory(configDir);
        _authRecordPath = Path.Combine(configDir, "azure_auth_record.json");
    }

    private AuthenticationRecord? LoadAuthenticationRecord()
    {
        try
        {
            if (File.Exists(_authRecordPath))
            {
                using var stream = File.OpenRead(_authRecordPath);
                return AuthenticationRecord.Deserialize(stream);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to load auth record from {Path}", _authRecordPath);
        }
        return null;
    }

    private void SaveAuthenticationRecord(AuthenticationRecord record)
    {
        try
        {
            // Serialize to a memory stream first, then use secure file creation
            using var memoryStream = new MemoryStream();
            record.Serialize(memoryStream);
            var content = System.Text.Encoding.UTF8.GetString(memoryStream.ToArray());
            AppPaths.CreateSecureFile(_authRecordPath, content);
            Log.Debug("Authentication record saved to {Path}", _authRecordPath);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to save auth record to {Path}", _authRecordPath);
        }
    }

    private void DeleteAuthenticationRecord()
    {
        try
        {
            if (File.Exists(_authRecordPath))
            {
                File.Delete(_authRecordPath);
                Log.Information("Authentication record deleted");
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to delete authentication record from {Path}", _authRecordPath);
        }
    }

    private InteractiveBrowserCredentialOptions CreateBrowserCredentialOptions()
    {
        return new InteractiveBrowserCredentialOptions
        {
            TenantId = "common",
            ClientId = "04b07795-8ddb-461a-bbee-02f9e1bf7b46", // Azure CLI
            AuthorityHost = AzureAuthorityHosts.AzurePublicCloud,
            TokenCachePersistenceOptions = _cacheOptions
        };
    }

    private DeviceCodeCredentialOptions CreateDeviceCodeCredentialOptions()
    {
        return new DeviceCodeCredentialOptions
        {
            TenantId = "common",
            ClientId = "04b07795-8ddb-461a-bbee-02f9e1bf7b46", // Azure CLI
            AuthorityHost = AzureAuthorityHosts.AzurePublicCloud,
            TokenCachePersistenceOptions = _cacheOptions,
            DeviceCodeCallback = (deviceCodeInfo, cancellation) =>
            {
                // Notify subscribers about the device code
                var info = new DeviceCodeInfo(
                    deviceCodeInfo.UserCode,
                    deviceCodeInfo.VerificationUri.ToString(),
                    deviceCodeInfo.Message);
                DeviceCodeRequired?.Invoke(this, info);
                Log.Information("Device code authentication required: {Message}", deviceCodeInfo.Message);
                return Task.CompletedTask;
            }
        };
    }

    public async Task<bool> TrySilentLoginAsync(CancellationToken ct = default)
    {
        // Load saved authentication record
        _authRecord = LoadAuthenticationRecord();
        if (_authRecord == null)
        {
            Log.Debug("No saved authentication record found, silent login not possible");
            return false;
        }

        Log.Debug("Found saved authentication record for user {Username}", _authRecord.Username);

        var context = new TokenRequestContext(
            new[] { "https://management.azure.com/.default" }
        );
        var serviceBusContext = new TokenRequestContext(
            new[] { "https://servicebus.azure.net/.default" }
        );

        // Try DeviceCodeCredential with saved auth record
        try
        {
            var silentDeviceCodeOptions = new DeviceCodeCredentialOptions
            {
                TenantId = _authRecord.TenantId,
                ClientId = "04b07795-8ddb-461a-bbee-02f9e1bf7b46",
                AuthorityHost = AzureAuthorityHosts.AzurePublicCloud,
                TokenCachePersistenceOptions = _cacheOptions,
                AuthenticationRecord = _authRecord,
                DisableAutomaticAuthentication = true
            };
            var deviceCodeCredential = new DeviceCodeCredential(silentDeviceCodeOptions);

            // This will use cached token if available
            _ = await deviceCodeCredential.GetTokenAsync(context, ct);
            _ = await deviceCodeCredential.GetTokenAsync(serviceBusContext, ct);

            _credential = deviceCodeCredential;
            _armClient = new ArmClient(_credential);
            IsAuthenticated = true;
            UserName = _authRecord.Username;

            AuthenticationChanged?.Invoke(this, true);
            Log.Information("Silent login succeeded for user {Username}", _authRecord.Username);
            return true;
        }
        catch (AuthenticationRequiredException)
        {
            Log.Debug("Cached token expired, interactive login required");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Silent login failed");
        }

        // Clear invalid auth record
        _authRecord = null;
        _credential = null;
        _armClient = null;
        IsAuthenticated = false;
        return false;
    }

    public async Task<bool> LoginAsync(CancellationToken ct = default)
    {
        var context = new TokenRequestContext(
            new[] { "https://management.azure.com/.default" }
        );

        // Try interactive browser first
        try
        {
            Log.Information("Attempting interactive browser authentication...");
            var browserOptions = CreateBrowserCredentialOptions();
            var browserCredential = new InteractiveBrowserCredential(browserOptions);

            // Authenticate and get the auth record for session persistence
            _authRecord = await browserCredential.AuthenticateAsync(context, ct);
            SaveAuthenticationRecord(_authRecord);

            _credential = browserCredential;
            _armClient = new ArmClient(_credential);
            IsAuthenticated = true;
            UserName = _authRecord.Username;

            Log.Information("Browser authentication successful for user {Username}", UserName);
            AuthenticationChanged?.Invoke(this, true);
            return true;
        }
        catch (Exception browserEx)
        {
            Log.Warning(browserEx, "Browser login failed ({ExceptionType}), falling back to device code authentication",
                browserEx.GetType().Name);

            // Fallback to device code authentication
            try
            {
                var deviceCodeOptions = CreateDeviceCodeCredentialOptions();
                var deviceCodeCredential = new DeviceCodeCredential(deviceCodeOptions);

                // Authenticate and get the auth record for session persistence
                _authRecord = await deviceCodeCredential.AuthenticateAsync(context, ct);
                SaveAuthenticationRecord(_authRecord);

                _credential = deviceCodeCredential;
                _armClient = new ArmClient(_credential);
                IsAuthenticated = true;
                UserName = _authRecord.Username;

                AuthenticationChanged?.Invoke(this, true);
                return true;
            }
            catch (Exception deviceCodeEx)
            {
                Log.Error(deviceCodeEx, "Device code login failed");
                IsAuthenticated = false;
                AuthenticationChanged?.Invoke(this, false);
                return false;
            }
        }
    }

    public Task LogoutAsync()
    {
        Log.Information("User {Username} logged out", UserName ?? "unknown");
        DeleteAuthenticationRecord();
        _authRecord = null;
        _credential = null;
        _armClient = null;
        IsAuthenticated = false;
        UserName = null;
        AuthenticationChanged?.Invoke(this, false);
        return Task.CompletedTask;
    }
}
