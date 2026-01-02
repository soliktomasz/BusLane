namespace BusLane.Services.Auth;

using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;

public class AzureAuthService : IAzureAuthService
{
    private InteractiveBrowserCredential? _credential;
    private ArmClient? _armClient;
    private readonly TokenCachePersistenceOptions _cacheOptions;

    public bool IsAuthenticated { get; private set; }
    public string? UserName { get; private set; }
    public TokenCredential? Credential => _credential;
    public ArmClient? ArmClient => _armClient;

    public event EventHandler<bool>? AuthenticationChanged;

    public AzureAuthService()
    {
        // Enable token cache persistence - tokens will be stored securely
        // On macOS: Keychain, On Windows: DPAPI, On Linux: encrypted file
        _cacheOptions = new TokenCachePersistenceOptions
        {
            Name = "BusLane"
        };
    }

    private InteractiveBrowserCredentialOptions CreateCredentialOptions()
    {
        return new InteractiveBrowserCredentialOptions
        {
            TenantId = "common",
            ClientId = "04b07795-8ddb-461a-bbee-02f9e1bf7b46", // Azure CLI
            AuthorityHost = AzureAuthorityHosts.AzurePublicCloud,
            TokenCachePersistenceOptions = _cacheOptions
        };
    }

    public async Task<bool> TrySilentLoginAsync(CancellationToken ct = default)
    {
        try
        {
            var options = CreateCredentialOptions();
            _credential = new InteractiveBrowserCredential(options);

            // Try to get a token silently (will use cached token if available)
            var context = new TokenRequestContext(
                new[] { "https://management.azure.com/.default" }
            );

            // Use GetTokenAsync - if there's a valid cached token, it won't prompt
            var token = await _credential.GetTokenAsync(context, ct);

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
        try
        {
            var options = CreateCredentialOptions();
            _credential = new InteractiveBrowserCredential(options);

            // Force authentication by requesting a token
            var context = new TokenRequestContext(
                new[] { "https://management.azure.com/.default" }
            );
            _ = await _credential.GetTokenAsync(context, ct);

            _armClient = new ArmClient(_credential);
            IsAuthenticated = true;
            UserName = "Azure User";

            AuthenticationChanged?.Invoke(this, true);
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Login failed: {ex.Message}");
            IsAuthenticated = false;
            AuthenticationChanged?.Invoke(this, false);
            return false;
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
