using System.Collections.ObjectModel;
using System.Text.Json;
using Avalonia.Platform.Storage;
using BusLane.Models;
using BusLane.Services.Abstractions;
using BusLane.Services.ServiceBus;
using BusLane.ViewModels.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BusLane.ViewModels;

public partial class SendMessageViewModel : ViewModelBase
{
    private readonly IServiceBusOperations _operations;
    private readonly IFileDialogService? _fileDialogService;
    private readonly string _entityName;
    private readonly Action _onClose;
    private readonly Action<string> _onStatusUpdate;

    private static readonly string SavedMessagesPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "BusLane",
        "saved_messages.json"
    );

    [ObservableProperty] private string _body = "";
    [ObservableProperty] private string? _contentType = "application/json";
    [ObservableProperty] private string? _correlationId;
    [ObservableProperty] private string? _messageId;
    [ObservableProperty] private string? _sessionId;
    [ObservableProperty] private string? _subject;
    [ObservableProperty] private string? _to;
    [ObservableProperty] private string? _replyTo;
    [ObservableProperty] private string? _replyToSessionId;
    [ObservableProperty] private string? _partitionKey;
    [ObservableProperty] private string? _timeToLiveText;
    [ObservableProperty] private string? _scheduledEnqueueTimeText;
    [ObservableProperty] private bool _isSending;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private string _saveMessageName = "";
    [ObservableProperty] private bool _showSaveDialog;
    [ObservableProperty] private bool _showLoadDialog;
    [ObservableProperty] private SavedMessage? _selectedSavedMessage;

    public ObservableCollection<CustomProperty> CustomProperties { get; } = new();
    public ObservableCollection<SavedMessage> SavedMessages { get; } = new();

    public string EntityName => _entityName;

    public SendMessageViewModel(
        IServiceBusOperations operations,
        string entityName,
        Action onClose,
        Action<string> onStatusUpdate,
        IFileDialogService? fileDialogService = null)
    {
        _operations = operations;
        _fileDialogService = fileDialogService;
        _entityName = entityName;
        _onClose = onClose;
        _onStatusUpdate = onStatusUpdate;

        LoadSavedMessages();
    }

    [RelayCommand]
    private void AddCustomProperty()
    {
        CustomProperties.Add(new CustomProperty());
    }

    [RelayCommand]
    private void RemoveCustomProperty(CustomProperty property)
    {
        CustomProperties.Remove(property);
    }

    [RelayCommand]
    private async Task SendAsync()
    {
        if (string.IsNullOrWhiteSpace(Body))
        {
            ErrorMessage = "Message body is required";
            return;
        }

        IsSending = true;
        ErrorMessage = null;

        try
        {
            TimeSpan? timeToLive = null;
            if (!string.IsNullOrWhiteSpace(TimeToLiveText))
            {
                if (TimeSpan.TryParse(TimeToLiveText, out var ttl))
                    timeToLive = ttl;
                else if (int.TryParse(TimeToLiveText, out var seconds))
                    timeToLive = TimeSpan.FromSeconds(seconds);
            }

            DateTimeOffset? scheduledTime = null;
            if (!string.IsNullOrWhiteSpace(ScheduledEnqueueTimeText))
            {
                if (DateTimeOffset.TryParse(ScheduledEnqueueTimeText, out var parsed))
                    scheduledTime = parsed;
            }

            var properties = new Dictionary<string, object>();
            foreach (var prop in CustomProperties.Where(p => !string.IsNullOrWhiteSpace(p.Key)))
            {
                properties[prop.Key] = prop.Value ?? "";
            }

            await _operations.SendMessageAsync(
                _entityName,
                Body,
                properties,
                ContentType,
                CorrelationId,
                MessageId,
                SessionId,
                Subject,
                To,
                ReplyTo,
                ReplyToSessionId,
                PartitionKey,
                timeToLive,
                scheduledTime
            );

            _onStatusUpdate("Message sent successfully");
            _onClose();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to send: {ex.Message}";
        }
        finally
        {
            IsSending = false;
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        _onClose();
    }

    [RelayCommand]
    private void ShowSave()
    {
        SaveMessageName = "";
        ShowSaveDialog = true;
    }

    [RelayCommand]
    private void HideSave()
    {
        ShowSaveDialog = false;
    }

    [RelayCommand]
    private void SaveMessage()
    {
        if (string.IsNullOrWhiteSpace(SaveMessageName))
        {
            ErrorMessage = "Please enter a name for the saved message";
            return;
        }

        var saved = new SavedMessage
        {
            Name = SaveMessageName,
            Body = Body,
            ContentType = ContentType,
            CorrelationId = CorrelationId,
            MessageId = MessageId,
            SessionId = SessionId,
            Subject = Subject,
            To = To,
            ReplyTo = ReplyTo,
            ReplyToSessionId = ReplyToSessionId,
            PartitionKey = PartitionKey,
            TimeToLive = !string.IsNullOrWhiteSpace(TimeToLiveText) && TimeSpan.TryParse(TimeToLiveText, out var ttl) ? ttl : null,
            ScheduledEnqueueTime = !string.IsNullOrWhiteSpace(ScheduledEnqueueTimeText) && DateTimeOffset.TryParse(ScheduledEnqueueTimeText, out var st) ? st : null,
            CustomProperties = CustomProperties
                .Where(p => !string.IsNullOrWhiteSpace(p.Key))
                .ToDictionary(p => p.Key, p => p.Value ?? "")
        };

        SavedMessages.Add(saved);
        PersistSavedMessages();
        ShowSaveDialog = false;
        _onStatusUpdate($"Message saved as '{SaveMessageName}'");
    }

    [RelayCommand]
    private void ShowLoad()
    {
        LoadSavedMessages();
        ShowLoadDialog = true;
    }

    [RelayCommand]
    private void HideLoad()
    {
        ShowLoadDialog = false;
    }

    [RelayCommand]
    private void LoadMessage(SavedMessage message)
    {
        Body = message.Body;
        ContentType = message.ContentType;
        CorrelationId = message.CorrelationId;
        MessageId = message.MessageId;
        SessionId = message.SessionId;
        Subject = message.Subject;
        To = message.To;
        ReplyTo = message.ReplyTo;
        ReplyToSessionId = message.ReplyToSessionId;
        PartitionKey = message.PartitionKey;
        TimeToLiveText = message.TimeToLive?.ToString();
        ScheduledEnqueueTimeText = message.ScheduledEnqueueTime?.ToString("O");

        CustomProperties.Clear();
        foreach (var prop in message.CustomProperties)
        {
            CustomProperties.Add(new CustomProperty { Key = prop.Key, Value = prop.Value });
        }

        ShowLoadDialog = false;
        _onStatusUpdate($"Loaded message '{message.Name}'");
    }

    [RelayCommand]
    private void DeleteSavedMessage(SavedMessage message)
    {
        SavedMessages.Remove(message);
        PersistSavedMessages();
    }

    /// <summary>
    /// File type filter for JSON files.
    /// </summary>
    private static readonly FilePickerFileType JsonFileType = new("JSON Files")
    {
        Patterns = new[] { "*.json" },
        MimeTypes = new[] { "application/json" }
    };

    /// <summary>
    /// Exports all saved messages to a JSON file.
    /// </summary>
    [RelayCommand]
    private async Task ExportMessagesAsync()
    {
        if (_fileDialogService == null)
        {
            ErrorMessage = "File dialog service not available";
            return;
        }

        if (SavedMessages.Count == 0)
        {
            ErrorMessage = "No saved messages to export";
            return;
        }

        try
        {
            var defaultFileName = $"BusLane_Messages_{DateTime.Now:yyyyMMdd_HHmmss}.json";
            var filePath = await _fileDialogService.SaveFileAsync(
                "Export Messages",
                defaultFileName,
                new[] { JsonFileType });

            if (string.IsNullOrEmpty(filePath)) return;

            var exportContainer = new MessageExportContainer
            {
                Description = $"Exported from BusLane on {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                Messages = SavedMessages.ToList()
            };

            var json = JsonSerializer.Serialize(exportContainer, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            await File.WriteAllTextAsync(filePath, json);
            _onStatusUpdate($"Exported {SavedMessages.Count} message(s) to {Path.GetFileName(filePath)}");
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to export: {ex.Message}";
        }
    }

    /// <summary>
    /// Exports a single saved message to a JSON file.
    /// </summary>
    [RelayCommand]
    private async Task ExportSingleMessageAsync(SavedMessage message)
    {
        if (_fileDialogService == null)
        {
            ErrorMessage = "File dialog service not available";
            return;
        }

        try
        {
            var safeName = string.Join("_", message.Name.Split(Path.GetInvalidFileNameChars()));
            var defaultFileName = $"{safeName}_{DateTime.Now:yyyyMMdd_HHmmss}.json";
            var filePath = await _fileDialogService.SaveFileAsync(
                "Export Message",
                defaultFileName,
                new[] { JsonFileType });

            if (string.IsNullOrEmpty(filePath)) return;

            var exportContainer = new MessageExportContainer
            {
                Description = $"Single message export: {message.Name}",
                Messages = new List<SavedMessage> { message }
            };

            var json = JsonSerializer.Serialize(exportContainer, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            await File.WriteAllTextAsync(filePath, json);
            _onStatusUpdate($"Exported message '{message.Name}' to {Path.GetFileName(filePath)}");
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to export: {ex.Message}";
        }
    }

    /// <summary>
    /// Imports messages from a JSON file and loads the first one into the form.
    /// </summary>
    [RelayCommand]
    private async Task ImportMessagesAsync()
    {
        if (_fileDialogService == null)
        {
            ErrorMessage = "File dialog service not available";
            return;
        }

        try
        {
            var filePath = await _fileDialogService.OpenFileAsync(
                "Import Messages",
                new[] { JsonFileType });

            if (string.IsNullOrEmpty(filePath)) return;

            var json = await File.ReadAllTextAsync(filePath);
            
            // Try to parse as MessageExportContainer first
            var importedMessages = new List<SavedMessage>();
            try
            {
                var container = JsonSerializer.Deserialize<MessageExportContainer>(json);
                if (container?.Messages != null)
                {
                    foreach (var msg in container.Messages)
                    {
                        // Generate new ID to avoid duplicates
                        msg.Id = Guid.NewGuid().ToString();
                        msg.CreatedAt = DateTime.UtcNow;
                        importedMessages.Add(msg);
                    }
                }
            }
            catch
            {
                // Fall back to trying to parse as a list of SavedMessage (legacy format)
                try
                {
                    var messages = JsonSerializer.Deserialize<List<SavedMessage>>(json);
                    if (messages != null)
                    {
                        foreach (var msg in messages)
                        {
                            msg.Id = Guid.NewGuid().ToString();
                            msg.CreatedAt = DateTime.UtcNow;
                            importedMessages.Add(msg);
                        }
                    }
                }
                catch
                {
                    // Try parsing as single SavedMessage
                    var singleMsg = JsonSerializer.Deserialize<SavedMessage>(json);
                    if (singleMsg != null)
                    {
                        singleMsg.Id = Guid.NewGuid().ToString();
                        singleMsg.CreatedAt = DateTime.UtcNow;
                        importedMessages.Add(singleMsg);
                    }
                }
            }

            if (importedMessages.Count > 0)
            {
                // Add all imported messages to SavedMessages collection
                foreach (var msg in importedMessages)
                {
                    SavedMessages.Add(msg);
                }
                PersistSavedMessages();
                
                // Load the first imported message into the form
                LoadMessage(importedMessages[0]);
                
                _onStatusUpdate($"Imported and loaded message from {Path.GetFileName(filePath)}");
            }
            else
            {
                ErrorMessage = "No valid messages found in the file";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to import: {ex.Message}";
        }
    }

    /// <summary>
    /// Populates the form with data from an existing message (for cloning).
    /// </summary>
    public void PopulateFromMessage(Models.MessageInfo message)
    {
        Body = message.Body;
        ContentType = message.ContentType;
        CorrelationId = message.CorrelationId;
        MessageId = null; // Don't copy MessageId - let it generate a new one
        SessionId = message.SessionId;
        Subject = message.Subject;
        To = message.To;
        ReplyTo = message.ReplyTo;
        ReplyToSessionId = message.ReplyToSessionId;
        PartitionKey = message.PartitionKey;
        TimeToLiveText = message.TimeToLive?.ToString();
        ScheduledEnqueueTimeText = null; // Don't copy scheduled time

        CustomProperties.Clear();
        foreach (var prop in message.Properties)
        {
            CustomProperties.Add(new CustomProperty { Key = prop.Key, Value = prop.Value?.ToString() ?? "" });
        }
    }

    [RelayCommand]
    private void ClearForm()
    {
        Body = "";
        ContentType = "application/json";
        CorrelationId = null;
        MessageId = null;
        SessionId = null;
        Subject = null;
        To = null;
        ReplyTo = null;
        ReplyToSessionId = null;
        PartitionKey = null;
        TimeToLiveText = null;
        ScheduledEnqueueTimeText = null;
        CustomProperties.Clear();
        ErrorMessage = null;
    }

    private void LoadSavedMessages()
    {
        SavedMessages.Clear();

        try
        {
            if (File.Exists(SavedMessagesPath))
            {
                var json = File.ReadAllText(SavedMessagesPath);
                var messages = JsonSerializer.Deserialize<List<SavedMessage>>(json);
                if (messages != null)
                {
                    foreach (var msg in messages.OrderByDescending(m => m.CreatedAt))
                    {
                        SavedMessages.Add(msg);
                    }
                }
            }
        }
        catch
        {
            // Ignore errors loading saved messages
        }
    }

    private void PersistSavedMessages()
    {
        try
        {
            var dir = Path.GetDirectoryName(SavedMessagesPath);
            if (dir != null && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var json = JsonSerializer.Serialize(SavedMessages.ToList(), new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(SavedMessagesPath, json);
        }
        catch
        {
            // Ignore errors saving messages
        }
    }
}

