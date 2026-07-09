namespace BusLane.ViewModels;

using System.Collections.ObjectModel;
using System.Text.Json;
using Avalonia.Platform.Storage;
using BusLane.Models;
using BusLane.Services.Abstractions;
using BusLane.Services.Infrastructure;
using BusLane.Services.ServiceBus;
using BusLane.Services.Templates;
using BusLane.ViewModels.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using static BusLane.Services.Infrastructure.SafeJsonSerializer;

public partial class SendMessageViewModel : ViewModelBase
{
    private readonly IServiceBusOperations _operations;
    private readonly IFileDialogService? _fileDialogService;
    private readonly string _entityName;
    private readonly Action _onClose;
    private readonly Action<string> _onStatusUpdate;
    private readonly string _savedMessagesPath;
    private readonly IScheduledMessageStore? _scheduledMessageStore;

    private static readonly string DefaultSavedMessagesPath = Path.Combine(
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
    [ObservableProperty] private string _saveMessageCategory = "";
    [ObservableProperty] private string _saveMessageTags = "";
    [ObservableProperty] private string _templateSearchQuery = "";
    [ObservableProperty] private bool _showSaveDialog;
    [ObservableProperty] private bool _showLoadDialog;
    [ObservableProperty] private SavedMessage? _selectedSavedMessage;
    [NotifyPropertyChangedFor(nameof(HasActiveTemplate))]
    [ObservableProperty] private SavedMessage? _activeTemplate;
    [ObservableProperty] private bool _isComposeTabSelected = true;
    [ObservableProperty] private bool _isPropertiesTabSelected;
    [ObservableProperty] private bool _isCustomTabSelected;

    public ObservableCollection<CustomProperty> CustomProperties { get; } = new();
    public ObservableCollection<SavedMessage> SavedMessages { get; } = new();
    public ObservableCollection<TemplateTokenValue> TemplateTokenValues { get; } = new();

    public string EntityName => _entityName;
    public IEnumerable<SavedMessage> FilteredSavedMessages => SavedMessages.Where(MatchesTemplateSearch);
    public bool HasActiveTemplate => ActiveTemplate != null;

    public SendMessageViewModel(
        IServiceBusOperations operations,
        string entityName,
        Action onClose,
        Action<string> onStatusUpdate,
        IFileDialogService? fileDialogService = null,
        string? savedMessagesPath = null,
        IScheduledMessageStore? scheduledMessageStore = null)
    {
        _operations = operations;
        _fileDialogService = fileDialogService;
        _entityName = entityName;
        _onClose = onClose;
        _onStatusUpdate = onStatusUpdate;
        _savedMessagesPath = savedMessagesPath ?? DefaultSavedMessagesPath;
        _scheduledMessageStore = scheduledMessageStore;

        LoadSavedMessages();
    }

    [RelayCommand]
    private void AddCustomProperty()
    {
        var prop = new CustomProperty();
        prop.PropertyChanged += OnCustomPropertyChanged;
        CustomProperties.Add(prop);
    }

    [RelayCommand]
    private void RemoveCustomProperty(CustomProperty property)
    {
        property.PropertyChanged -= OnCustomPropertyChanged;
        CustomProperties.Remove(property);
        if (ActiveTemplate != null)
        {
            var message = BuildSavedMessageFromForm();
            RefreshTemplateTokenValues(message);
        }
    }

    private void OnCustomPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (ActiveTemplate != null)
        {
            var message = BuildSavedMessageFromForm();
            RefreshTemplateTokenValues(message);
        }
    }

    [RelayCommand]
    private async Task SendAsync()
    {
        var templateValues = TemplateTokenValues.ToDictionary(t => t.Name, t => (string?)t.Value, StringComparer.OrdinalIgnoreCase);
        var currentMessage = BuildSavedMessageFromForm();
        var missingTokens = MessageTemplateEngine.FindMissingTokenValues(currentMessage, templateValues);
        if (missingTokens.Count > 0)
        {
            ErrorMessage = $"Missing template values: {string.Join(", ", missingTokens)}";
            return;
        }

        var messageToSend = MessageTemplateEngine.Apply(currentMessage, templateValues);

        if (string.IsNullOrWhiteSpace(Body))
        {
            ErrorMessage = "Message body is required";
            return;
        }

        IsSending = true;
        ErrorMessage = null;

        try
        {
            var properties = new Dictionary<string, object>();
            foreach (var prop in messageToSend.CustomProperties.Where(p => !string.IsNullOrWhiteSpace(p.Key)))
            {
                properties[prop.Key] = prop.Value;
            }

            if (messageToSend.ScheduledEnqueueTime.HasValue)
            {
                var sequenceNumber = await _operations.ScheduleMessageAsync(
                    _entityName,
                    messageToSend.Body,
                    properties,
                    messageToSend.ScheduledEnqueueTime.Value,
                    messageToSend.ContentType,
                    messageToSend.CorrelationId,
                    messageToSend.MessageId,
                    messageToSend.SessionId,
                    messageToSend.Subject,
                    messageToSend.To,
                    messageToSend.ReplyTo,
                    messageToSend.ReplyToSessionId,
                    messageToSend.PartitionKey,
                    messageToSend.TimeToLive
                );

                if (_scheduledMessageStore != null)
                {
                    try
                    {
                        await _scheduledMessageStore.AddAsync(new ScheduledMessageIndexEntry(
                            _entityName,
                            SubscriptionName: null,
                            sequenceNumber,
                            messageToSend.ScheduledEnqueueTime.Value,
                            messageToSend.MessageId,
                            BuildBodyPreview(messageToSend.Body),
                            DateTimeOffset.UtcNow));
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Failed to update scheduled message index for {EntityName}", _entityName);
                    }
                }

                _onStatusUpdate($"Message scheduled successfully (sequence {sequenceNumber})");
            }
            else
            {
                await _operations.SendMessageAsync(
                    _entityName,
                    messageToSend.Body,
                    properties,
                    messageToSend.ContentType,
                    messageToSend.CorrelationId,
                    messageToSend.MessageId,
                    messageToSend.SessionId,
                    messageToSend.Subject,
                    messageToSend.To,
                    messageToSend.ReplyTo,
                    messageToSend.ReplyToSessionId,
                    messageToSend.PartitionKey,
                    messageToSend.TimeToLive,
                    null
                );

                _onStatusUpdate("Message sent successfully");
            }
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
        SaveMessageCategory = "";
        SaveMessageTags = "";
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

        var saved = BuildSavedMessageFromForm();
        saved.Name = SaveMessageName;
        saved.Category = SaveMessageCategory.Trim();
        saved.Tags = ParseTags(SaveMessageTags);
        saved.TokenValues = TemplateTokenValues
            .Where(t => !string.IsNullOrWhiteSpace(t.Name) && !string.IsNullOrWhiteSpace(t.Value))
            .ToDictionary(t => t.Name, t => t.Value);

        SavedMessages.Add(saved);
        OnPropertyChanged(nameof(FilteredSavedMessages));
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
        ActiveTemplate = message;
        RefreshTemplateTokenValues(message);

        CustomProperties.Clear();
        foreach (var prop in message.CustomProperties)
        {
            var customProp = new CustomProperty { Key = prop.Key, Value = prop.Value };
            customProp.PropertyChanged += OnCustomPropertyChanged;
            CustomProperties.Add(customProp);
        }

        ShowLoadDialog = false;
        _onStatusUpdate($"Loaded message '{message.Name}'");
    }

    [RelayCommand]
    private void DeleteSavedMessage(SavedMessage message)
    {
        SavedMessages.Remove(message);
        OnPropertyChanged(nameof(FilteredSavedMessages));
        PersistSavedMessages();
    }

    [RelayCommand]
    private void DuplicateSavedMessage(SavedMessage message)
    {
        var duplicate = message.Duplicate();
        SavedMessages.Add(duplicate);
        OnPropertyChanged(nameof(FilteredSavedMessages));
        PersistSavedMessages();
        _onStatusUpdate($"Duplicated template '{message.Name}'");
    }

    [RelayCommand]
    private void UpdateActiveTemplate()
    {
        if (ActiveTemplate == null)
        {
            ErrorMessage = "Load a template before updating it";
            return;
        }

        var updated = BuildSavedMessageFromForm();
        ActiveTemplate.Body = updated.Body;
        ActiveTemplate.ContentType = updated.ContentType;
        ActiveTemplate.CorrelationId = updated.CorrelationId;
        ActiveTemplate.MessageId = updated.MessageId;
        ActiveTemplate.SessionId = updated.SessionId;
        ActiveTemplate.Subject = updated.Subject;
        ActiveTemplate.To = updated.To;
        ActiveTemplate.ReplyTo = updated.ReplyTo;
        ActiveTemplate.ReplyToSessionId = updated.ReplyToSessionId;
        ActiveTemplate.PartitionKey = updated.PartitionKey;
        ActiveTemplate.TimeToLive = updated.TimeToLive;
        ActiveTemplate.ScheduledEnqueueTime = updated.ScheduledEnqueueTime;
        ActiveTemplate.CustomProperties = updated.CustomProperties;
        ActiveTemplate.TokenValues = TemplateTokenValues
            .Where(t => !string.IsNullOrWhiteSpace(t.Name) && !string.IsNullOrWhiteSpace(t.Value))
            .ToDictionary(t => t.Name, t => t.Value);

        OnPropertyChanged(nameof(FilteredSavedMessages));
        PersistSavedMessages();
        _onStatusUpdate($"Updated template '{ActiveTemplate.Name}'");
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

            var json = Serialize(exportContainer);
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

            var json = Serialize(exportContainer);
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
                var container = Deserialize<MessageExportContainer>(json);
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
                    var messages = Deserialize<List<SavedMessage>>(json);
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
                    var singleMsg = Deserialize<SavedMessage>(json);
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
            var customProp = new CustomProperty { Key = prop.Key, Value = prop.Value?.ToString() ?? "" };
            customProp.PropertyChanged += OnCustomPropertyChanged;
            CustomProperties.Add(customProp);
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
        TemplateTokenValues.Clear();
        ActiveTemplate = null;
        ErrorMessage = null;
    }

    private void LoadSavedMessages()
    {
        SavedMessages.Clear();

        try
        {
            if (File.Exists(_savedMessagesPath))
            {
                var json = File.ReadAllText(_savedMessagesPath);
                var messages = Deserialize<List<SavedMessage>>(json);
                if (messages != null)
                {
                    foreach (var msg in messages.OrderByDescending(m => m.CreatedAt))
                    {
                        SavedMessages.Add(msg);
                    }
                }
            }
            OnPropertyChanged(nameof(FilteredSavedMessages));
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
            var json = Serialize(SavedMessages.ToList());
            AppPaths.CreateSecureFile(_savedMessagesPath, json);
        }
        catch
        {
            // Ignore errors saving messages
        }
    }

    partial void OnTemplateSearchQueryChanged(string value)
    {
        OnPropertyChanged(nameof(FilteredSavedMessages));
    }

    partial void OnBodyChanged(string value)
    {
        if (ActiveTemplate != null)
        {
            var message = BuildSavedMessageFromForm();
            RefreshTemplateTokenValues(message);
        }
    }

    partial void OnContentTypeChanged(string? value)
    {
        if (ActiveTemplate != null)
        {
            var message = BuildSavedMessageFromForm();
            RefreshTemplateTokenValues(message);
        }
    }

    partial void OnCorrelationIdChanged(string? value)
    {
        if (ActiveTemplate != null)
        {
            var message = BuildSavedMessageFromForm();
            RefreshTemplateTokenValues(message);
        }
    }

    partial void OnMessageIdChanged(string? value)
    {
        if (ActiveTemplate != null)
        {
            var message = BuildSavedMessageFromForm();
            RefreshTemplateTokenValues(message);
        }
    }

    partial void OnSessionIdChanged(string? value)
    {
        if (ActiveTemplate != null)
        {
            var message = BuildSavedMessageFromForm();
            RefreshTemplateTokenValues(message);
        }
    }

    partial void OnSubjectChanged(string? value)
    {
        if (ActiveTemplate != null)
        {
            var message = BuildSavedMessageFromForm();
            RefreshTemplateTokenValues(message);
        }
    }

    partial void OnToChanged(string? value)
    {
        if (ActiveTemplate != null)
        {
            var message = BuildSavedMessageFromForm();
            RefreshTemplateTokenValues(message);
        }
    }

    partial void OnReplyToChanged(string? value)
    {
        if (ActiveTemplate != null)
        {
            var message = BuildSavedMessageFromForm();
            RefreshTemplateTokenValues(message);
        }
    }

    partial void OnReplyToSessionIdChanged(string? value)
    {
        if (ActiveTemplate != null)
        {
            var message = BuildSavedMessageFromForm();
            RefreshTemplateTokenValues(message);
        }
    }

    partial void OnPartitionKeyChanged(string? value)
    {
        if (ActiveTemplate != null)
        {
            var message = BuildSavedMessageFromForm();
            RefreshTemplateTokenValues(message);
        }
    }

    private SavedMessage BuildSavedMessageFromForm()
    {
        return new SavedMessage
        {
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
            TimeToLive = ParseTimeToLive(),
            ScheduledEnqueueTime = ParseScheduledEnqueueTime(),
            CustomProperties = CustomProperties
                .Where(p => !string.IsNullOrWhiteSpace(p.Key))
                .ToDictionary(p => p.Key, p => p.Value ?? "")
        };
    }

    private TimeSpan? ParseTimeToLive()
    {
        if (string.IsNullOrWhiteSpace(TimeToLiveText))
        {
            return null;
        }

        if (TimeSpan.TryParse(TimeToLiveText, out var ttl))
        {
            return ttl;
        }

        return int.TryParse(TimeToLiveText, out var seconds) ? TimeSpan.FromSeconds(seconds) : null;
    }

    private DateTimeOffset? ParseScheduledEnqueueTime()
    {
        if (string.IsNullOrWhiteSpace(ScheduledEnqueueTimeText))
        {
            return null;
        }

        return DateTimeOffset.TryParse(ScheduledEnqueueTimeText, out var scheduledTime) ? scheduledTime : null;
    }

    private static string BuildBodyPreview(string body)
    {
        const int maxPreviewLength = 200;
        return body.Length <= maxPreviewLength
            ? body
            : body[..maxPreviewLength];
    }

    private void RefreshTemplateTokenValues(SavedMessage message)
    {
        var existingValues = TemplateTokenValues.ToDictionary(t => t.Name, t => t.Value, StringComparer.OrdinalIgnoreCase);
        TemplateTokenValues.Clear();
        foreach (var token in MessageTemplateEngine.ExtractTokenNames(message))
        {
            string? value = null;
            if (!existingValues.TryGetValue(token, out value))
            {
                message.TokenValues.TryGetValue(token, out value);
            }
            TemplateTokenValues.Add(new TemplateTokenValue { Name = token, Value = value ?? "" });
        }
    }

    private bool MatchesTemplateSearch(SavedMessage message)
    {
        if (string.IsNullOrWhiteSpace(TemplateSearchQuery))
        {
            return true;
        }

        var query = TemplateSearchQuery.Trim();
        return Contains(message.Name, query)
            || Contains(message.Category, query)
            || message.Tags.Any(tag => Contains(tag, query))
            || Contains(message.Body, query)
            || Contains(message.ContentType, query)
            || message.CustomProperties.Any(prop => Contains(prop.Key, query) || Contains(prop.Value, query));
    }

    private static bool Contains(string? value, string query)
    {
        return value?.Contains(query, StringComparison.OrdinalIgnoreCase) == true;
    }

    private static List<string> ParseTags(string tags)
    {
        return tags
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
