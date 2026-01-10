namespace BusLane.Services.Auth;

using Azure.Core;
using Azure.ResourceManager;

/// <summary>
/// Contains information about a device code authentication request.
/// </summary>
public record DeviceCodeInfo(string UserCode, string VerificationUri, string Message);

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

    /// <summary>
    /// Event raised when device code authentication requires user action.
    /// </summary>
    event EventHandler<DeviceCodeInfo>? DeviceCodeRequired;
}
