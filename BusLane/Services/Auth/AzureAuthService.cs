namespace BusLane.Services.Auth;

using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;

public class AzureAuthService : IAzureAuthService
{
    private TokenCredential? _credential;
    private ArmClient? _armClient;
    private readonly TokenCachePersistenceOptions _cacheOptions;

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
        try
        {
            var options = CreateBrowserCredentialOptions();
            var browserCredential = new InteractiveBrowserCredential(options);

            // Try to get a token silently (will use cached token if available)
            var context = new TokenRequestContext(
                new[] { "https://management.azure.com/.default" }
            );

            // Use GetTokenAsync - if there's a valid cached token, it won't prompt
            var token = await browserCredential.GetTokenAsync(context, ct);

            _credential = browserCredential;
            _armClient = new ArmClient(_credential);
            IsAuthenticated = true;
            UserName = "Azure User";

            AuthenticationChanged?.Invoke(this, true);
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Silent login failed: {ex.Message}");
            // Silent login failed - user needs to do interactive login
            _credential = null;
            _armClient = null;
            IsAuthenticated = false;
            return false;
        }
    }

    public async Task<bool> LoginAsync(CancellationToken ct = default)
    {
        // Try interactive browser first
        try
        {
            var browserOptions = CreateBrowserCredentialOptions();
            var browserCredential = new InteractiveBrowserCredential(browserOptions);

            var context = new TokenRequestContext(
                new[] { "https://management.azure.com/.default" }
            );
            _ = await browserCredential.GetTokenAsync(context, ct);

            _credential = browserCredential;
            _armClient = new ArmClient(_credential);
            IsAuthenticated = true;
            UserName = "Azure User";

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

                var context = new TokenRequestContext(
                    new[] { "https://management.azure.com/.default" }
                );
                _ = await deviceCodeCredential.GetTokenAsync(context, ct);

                _credential = deviceCodeCredential;
                _armClient = new ArmClient(_credential);
                IsAuthenticated = true;
                UserName = "Azure User";

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
        _credential = null;
        _armClient = null;
        IsAuthenticated = false;
        UserName = null;
        AuthenticationChanged?.Invoke(this, false);
        return Task.CompletedTask;
    }
}
