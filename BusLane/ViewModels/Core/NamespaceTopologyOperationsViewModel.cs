namespace BusLane.ViewModels.Core;

using BusLane.Models;
using BusLane.Services.Abstractions;
using BusLane.Services.ServiceBus;
using Avalonia.Platform.Storage;

/// <summary>
/// Coordinates namespace topology import and export UI workflows.
/// </summary>
public sealed class NamespaceTopologyOperationsViewModel
{
    private static readonly FilePickerFileType TopologyJsonFileType = new("JSON Files")
    {
        Patterns = ["*.json"],
        MimeTypes = ["application/json"]
    };

    private readonly Func<IServiceBusOperations?> _getOperations;
    private readonly Func<IFileDialogService?> _getFileDialogService;
    private readonly INamespaceTopologyService? _topologyService;
    private readonly ConfirmationDialogViewModel _confirmation;
    private readonly Action<string> _setStatusMessage;
    private readonly Action<bool> _setLoading;
    private readonly Func<Task> _refreshAsync;

    public NamespaceTopologyOperationsViewModel(
        Func<IServiceBusOperations?> getOperations,
        Func<IFileDialogService?> getFileDialogService,
        INamespaceTopologyService? topologyService,
        ConfirmationDialogViewModel confirmation,
        Action<string> setStatusMessage,
        Action<bool> setLoading,
        Func<Task> refreshAsync)
    {
        _getOperations = getOperations;
        _getFileDialogService = getFileDialogService;
        _topologyService = topologyService;
        _confirmation = confirmation;
        _setStatusMessage = setStatusMessage;
        _setLoading = setLoading;
        _refreshAsync = refreshAsync;
    }

    /// <summary>
    /// Exports current namespace topology to a user-selected JSON file.
    /// </summary>
    /// <param name="ct">Cancellation token for export and file operations.</param>
    /// <returns>A task representing the export workflow.</returns>
    public async Task ExportAsync(CancellationToken ct = default)
    {
        var operations = _getOperations();
        var fileDialogService = _getFileDialogService();
        if (operations == null || _topologyService == null || fileDialogService == null)
        {
            _setStatusMessage("Topology export requires an active connection and file dialog support");
            return;
        }

        try
        {
            var defaultName = $"BusLane_Topology_{DateTime.Now:yyyyMMdd_HHmmss}.json";
            var filePath = await fileDialogService.SaveFileAsync(
                "Export Namespace Topology",
                defaultName,
                [TopologyJsonFileType]);
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return;
            }

            _setLoading(true);
            _setStatusMessage("Exporting namespace topology...");
            var document = await _topologyService.ExportAsync(operations, ct);
            await File.WriteAllTextAsync(filePath, NamespaceTopologySerializer.Serialize(document), ct);
            _setStatusMessage($"Exported namespace topology to {Path.GetFileName(filePath)}");
        }
        catch (Exception ex)
        {
            _setStatusMessage($"Unable to export topology: {ex.Message}");
        }
        finally
        {
            _setLoading(false);
        }
    }

    /// <summary>
    /// Imports a user-selected topology JSON file and prepares a confirmed apply plan.
    /// </summary>
    /// <param name="ct">Cancellation token for import and topology operations.</param>
    /// <returns>A task representing the import workflow.</returns>
    public async Task ImportAsync(CancellationToken ct = default)
    {
        var operations = _getOperations();
        var fileDialogService = _getFileDialogService();
        if (operations == null || _topologyService == null || fileDialogService == null)
        {
            _setStatusMessage("Topology import requires an active connection and file dialog support");
            return;
        }

        try
        {
            var filePath = await fileDialogService.OpenFileAsync("Import Namespace Topology", [TopologyJsonFileType]);
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return;
            }

            _setLoading(true);
            _setStatusMessage("Comparing namespace topology...");
            var document = NamespaceTopologySerializer.Deserialize(await File.ReadAllTextAsync(filePath, ct));
            var plan = await _topologyService.BuildImportPlanAsync(operations, document, ct);
            var changeCount = plan.Actions.Count(action => action.ActionType != TopologyImportActionType.Skip);
            if (changeCount == 0)
            {
                _setStatusMessage("Topology import dry-run found no changes");
                return;
            }

            var summary = string.Join(Environment.NewLine, plan.Actions
                .Where(action => action.ActionType != TopologyImportActionType.Skip)
                .Take(12)
                .Select(action => $"- {action.Description}"));
            if (changeCount > 12)
            {
                summary += Environment.NewLine + $"- ...and {changeCount - 12} more action(s)";
            }

            _confirmation.ShowConfirmation(
                "Apply Topology Import",
                $"Dry-run found {changeCount} non-destructive action(s):{Environment.NewLine}{summary}",
                "Apply",
                async () =>
                {
                    if (!ReferenceEquals(_getOperations(), operations))
                    {
                        _setStatusMessage("Unable to apply topology import: active connection changed");
                        return;
                    }

                    _setLoading(true);
                    try
                    {
                        await _topologyService.ApplyImportPlanAsync(operations, document, plan, ct);
                        _setStatusMessage($"Applied {changeCount} topology action(s)");
                        await _refreshAsync();
                    }
                    catch (Exception ex)
                    {
                        _setStatusMessage($"Unable to apply topology import: {ex.Message}");
                    }
                    finally
                    {
                        _setLoading(false);
                    }
                });
        }
        catch (Exception ex)
        {
            _setStatusMessage($"Unable to import topology: {ex.Message}");
        }
        finally
        {
            _setLoading(false);
        }
    }
}
