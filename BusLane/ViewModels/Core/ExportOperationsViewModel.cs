using BusLane.Models;
using BusLane.Services.Abstractions;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BusLane.ViewModels.Core;

/// <summary>
/// Handles exporting messages to JSON files.
/// </summary>
public partial class ExportOperationsViewModel : ViewModelBase
{
    private readonly Func<NavigationState> _getNavigation;
    private readonly IFileDialogService? _fileDialogService;
    private readonly Action<string> _setStatus;

    [ObservableProperty] private bool _isExporting;

    public ExportOperationsViewModel(
        Func<NavigationState> getNavigation,
        IFileDialogService? fileDialogService,
        Action<string> setStatus)
    {
        _getNavigation = getNavigation;
        _fileDialogService = fileDialogService;
        _setStatus = setStatus;
    }

    public async Task ExportMessageAsync(MessageInfo message)
    {
        if (_fileDialogService == null)
        {
            _setStatus("File dialog service not available");
            return;
        }

        IsExporting = true;

        try
        {
            var safeName = string.Join("_", (message.MessageId ?? "message").Split(Path.GetInvalidFileNameChars()));
            var defaultFileName = $"Message_{safeName}_{DateTime.Now:yyyyMMdd_HHmmss}.json";
            var filePath = await _fileDialogService.SaveFileAsync("Export Message", defaultFileName, new[] {
                new Avalonia.Platform.Storage.FilePickerFileType("JSON Files")
                {
                    Patterns = new[] { "*.json" },
                    MimeTypes = new[] { "application/json" }
                }
            });

            if (string.IsNullOrEmpty(filePath))
            {
                IsExporting = false;
                return;
            }

            var navigation = _getNavigation();
            var exportMessage = new SavedMessage
            {
                Name = $"Exported: {message.MessageId}",
                Body = message.Body,
                ContentType = message.ContentType,
                CorrelationId = message.CorrelationId,
                MessageId = message.MessageId,
                SessionId = message.SessionId,
                Subject = message.Subject,
                To = message.To,
                ReplyTo = message.ReplyTo,
                ReplyToSessionId = message.ReplyToSessionId,
                PartitionKey = message.PartitionKey,
                TimeToLive = message.TimeToLive,
                ScheduledEnqueueTime = message.ScheduledEnqueueTime,
                CustomProperties = message.Properties?.ToDictionary(p => p.Key, p => p.Value?.ToString() ?? "") ?? new Dictionary<string, string>(),
                CreatedAt = DateTime.UtcNow
            };

            var exportContainer = new MessageExportContainer
            {
                Description = $"Exported message from {navigation.SelectedQueue?.Name ?? navigation.SelectedSubscription?.Name}",
                Messages = new List<SavedMessage> { exportMessage }
            };

            var json = System.Text.Json.JsonSerializer.Serialize(exportContainer, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(filePath, json);
            _setStatus($"Exported message to {Path.GetFileName(filePath)}");
        }
        catch (Exception ex)
        {
            _setStatus($"Failed to export message: {ex.Message}");
        }
        finally
        {
            IsExporting = false;
        }
    }
}
