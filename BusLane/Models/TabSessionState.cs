// BusLane/Models/TabSessionState.cs
using BusLane.ViewModels;

namespace BusLane.Models;

/// <summary>
/// Represents the persisted state of a connection tab for session restore.
/// </summary>
public record TabSessionState
{
    /// <summary>
    /// Unique identifier for the tab.
    /// </summary>
    public required string TabId { get; init; }

    /// <summary>
    /// The connection mode used by this tab.
    /// </summary>
    public required ConnectionMode Mode { get; init; }

    /// <summary>
    /// ID of the saved connection (for ConnectionString mode).
    /// </summary>
    public string? ConnectionId { get; init; }

    /// <summary>
    /// Azure Resource ID of the namespace (for AzureAccount mode).
    /// </summary>
    public string? NamespaceId { get; init; }

    /// <summary>
    /// Name of the selected entity when the session was saved.
    /// </summary>
    public string? SelectedEntityName { get; init; }

    /// <summary>
    /// Position of this tab in the tab bar.
    /// </summary>
    public int TabOrder { get; init; }
}
