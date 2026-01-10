namespace BusLane.Services.Auth;

using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;

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
            Console.WriteLine($"Failed to load auth record: {ex.Message}");
        }
        return null;
    }

    private void SaveAuthenticationRecord(AuthenticationRecord record)
    {
        try
        {
            using var stream = File.Create(_authRecordPath);
            record.Serialize(stream);
            Console.WriteLine("Authentication record saved");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to save auth record: {ex.Message}");
        }
    }

    private void DeleteAuthenticationRecord()
    {
        try
        {
            if (File.Exists(_authRecordPath))
            {
                File.Delete(_authRecordPath);
                Console.WriteLine("Authentication record deleted");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to delete auth record: {ex.Message}");
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
                Console.WriteLine(deviceCodeInfo.Message);
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
            Console.WriteLine("No saved authentication record found");
            return false;
        }

        Console.WriteLine($"Found saved auth record for: {_authRecord.Username}");

        var context = new TokenRequestContext(
            new[] { "https://management.azure.com/.default" }
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
            var token = await deviceCodeCredential.GetTokenAsync(context, ct);

            _credential = deviceCodeCredential;
            _armClient = new ArmClient(_credential);
            IsAuthenticated = true;
            UserName = _authRecord.Username;

            AuthenticationChanged?.Invoke(this, true);
            Console.WriteLine($"Silent login succeeded for: {_authRecord.Username}");
            return true;
        }
        catch (AuthenticationRequiredException)
        {
            Console.WriteLine("Cached token expired, interactive login required");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Silent login failed: {ex.Message}");
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
            var browserOptions = CreateBrowserCredentialOptions();
            var browserCredential = new InteractiveBrowserCredential(browserOptions);

            // Authenticate and get the auth record for session persistence
            _authRecord = await browserCredential.AuthenticateAsync(context, ct);
            SaveAuthenticationRecord(_authRecord);

            _credential = browserCredential;
            _armClient = new ArmClient(_credential);
            IsAuthenticated = true;
            UserName = _authRecord.Username;

            AuthenticationChanged?.Invoke(this, true);
            return true;
        }
        catch (Exception browserEx)
        {
            Console.WriteLine($"Browser login failed: {browserEx.Message}");
            Console.WriteLine("Falling back to device code authentication...");

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
                Console.WriteLine($"Device code login failed: {deviceCodeEx.Message}");
                IsAuthenticated = false;
                AuthenticationChanged?.Invoke(this, false);
                return false;
            }
        }
    }

    public Task LogoutAsync()
    {
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
