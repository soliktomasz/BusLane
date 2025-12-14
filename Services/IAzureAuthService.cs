using Azure.Core;
using Azure.ResourceManager;

namespace BusLane.Services;

public interface IAzureAuthService
{
    Task<bool> LoginAsync(CancellationToken ct = default);
    Task<bool> TrySilentLoginAsync(CancellationToken ct = default);
    Task LogoutAsync();
    bool IsAuthenticated { get; }
    string? UserName { get; }
    TokenCredential? Credential { get; }
    ArmClient? ArmClient { get; }
    event EventHandler<bool>? AuthenticationChanged;
}
