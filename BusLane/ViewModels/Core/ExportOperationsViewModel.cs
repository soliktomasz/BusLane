namespace BusLane.ViewModels.Core;

using BusLane.Models;
using BusLane.Services.Abstractions;
using CommunityToolkit.Mvvm.ComponentModel;

/// <summary>
/// Handles exporting messages to JSON files.
/// </summary>
public partial class ExportOperationsViewModel : ViewModelBase
{
    private readonly Func<NavigationState> _getNavigation;
    private readonly Func<IFileDialogService?> _getFileDialogService;
    private readonly Action<string> _setStatus;

    [ObservableProperty] private bool _isExporting;

    public ExportOperationsViewModel(
        Func<NavigationState> getNavigation,
        Func<IFileDialogService?> getFileDialogService,
        Action<string> setStatus)
    {
        _getNavigation = getNavigation;
        _getFileDialogService = getFileDialogService;
        _setStatus = setStatus;
    }

    public async Task ExportMessageAsync(MessageInfo message)
    {
        var fileDialogService = _getFileDialogService();
        if (fileDialogService == null)
        {
            _setStatus("File dialog service not available");
            return;
        }

        IsExporting = true;

        try
        {
            var safeName = string.Join("_", (message.MessageId ?? "message").Split(Path.GetInvalidFileNameChars()));
            var defaultFileName = $"Message_{safeName}_{DateTime.Now:yyyyMMdd_HHmmss}.json";
            var filePath = await fileDialogService.SaveFileAsync("Export Message", defaultFileName, new[] {
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
            var exportContainer = new MessageExportContainer
            {
                Description = $"Exported message from {navigation.SelectedQueue?.Name ?? navigation.SelectedSubscription?.Name}",
                Messages = [CreateSavedMessage(message)]
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

    /// <summary>
    /// Exports selected messages to one JSON file, either with sendable metadata or as bodies only.
    /// </summary>
    public async Task ExportSelectedMessagesAsync(IReadOnlyCollection<MessageInfo> messages, bool bodyOnly)
    {
        if (messages.Count == 0)
        {
            _setStatus("No messages selected");
            return;
        }

        var fileDialogService = _getFileDialogService();
        if (fileDialogService == null)
        {
            _setStatus("File dialog service not available");
            return;
        }

        IsExporting = true;

        try
        {
            var defaultFileName = $"Messages_{messages.Count}_{DateTime.Now:yyyyMMdd_HHmmss}.json";
            var filePath = await fileDialogService.SaveFileAsync("Export Selected Messages", defaultFileName, new[] {
                new Avalonia.Platform.Storage.FilePickerFileType("JSON Files")
                {
                    Patterns = new[] { "*.json" },
                    MimeTypes = new[] { "application/json" }
                }
            });

            if (string.IsNullOrEmpty(filePath))
                return;

            string content;
            if (bodyOnly)
            {
                content = SerializeMessageBodies(messages);
            }
            else
            {
                var navigation = _getNavigation();
                var export = new MessageExportContainer
                {
                    Description = $"Exported messages from {navigation.SelectedQueue?.Name ?? navigation.SelectedSubscription?.Name}",
                    Messages = messages.Select(CreateSavedMessage).ToList()
                };
                content = System.Text.Json.JsonSerializer.Serialize(export, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            }

            await File.WriteAllTextAsync(filePath, content);
            _setStatus($"Exported {messages.Count} selected message(s) to {Path.GetFileName(filePath)}");
        }
        catch (Exception ex)
        {
            _setStatus($"Failed to export selected messages: {ex.Message}");
        }
        finally
        {
            IsExporting = false;
        }
    }

    private static string SerializeMessageBodies(IReadOnlyCollection<MessageInfo> messages)
    {
        if (messages.Count == 1)
            return messages.First().Body;

        var jsonBodies = new List<System.Text.Json.JsonElement>(messages.Count);
        foreach (var message in messages)
        {
            try
            {
                using var document = System.Text.Json.JsonDocument.Parse(message.Body);
                jsonBodies.Add(document.RootElement.Clone());
            }
            catch (System.Text.Json.JsonException)
            {
                return string.Join(Environment.NewLine + Environment.NewLine, messages.Select(item => item.Body));
            }
        }

        return System.Text.Json.JsonSerializer.Serialize(jsonBodies, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
    }

    private static SavedMessage CreateSavedMessage(MessageInfo message)
    {
        return new SavedMessage
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
            CustomProperties = message.Properties.ToDictionary(p => p.Key, p => p.Value?.ToString() ?? ""),
            CreatedAt = DateTime.UtcNow
        };
    }
}
