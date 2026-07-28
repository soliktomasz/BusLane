namespace BusLane.ViewModels;

using System.Collections.ObjectModel;
using BusLane.Models;
using BusLane.Services.ServiceBus;
using BusLane.ViewModels.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

public partial class ReplayMessageViewModel : ViewModelBase
{
    private readonly IMessageReplayService _replayService;
    private readonly Func<IServiceBusOperations?> _getOperations;
    private readonly ReplayRequest _initialRequest;
    private readonly IReadOnlyDictionary<string, object> _sourceProperties;

    [ObservableProperty] private ReplayDestination? _selectedDestination;
    [ObservableProperty] private string _body;
    [ObservableProperty] private string? _contentType;
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
    [ObservableProperty] private int _rateLimitPerSecond = 1;
    [ObservableProperty] private bool _isConfirmed;
    [ObservableProperty] private bool _isProductionAcknowledged;
    [ObservableProperty] private bool _hasPreview;
    [ObservableProperty] private bool _isSending;
    [ObservableProperty] private ReplayPreview? _preview;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private string? _resultMessage;
    [ObservableProperty] private string? _auditWarning;

    public CorrelationMessage Source { get; }
    public IReadOnlyList<ReplayDestination> AvailableDestinations { get; }
    public ObservableCollection<CustomProperty> CustomProperties { get; } = [];
    public bool IsProductionDestination =>
        SelectedDestination?.Environment == ConnectionEnvironment.Production;

    public ReplayMessageViewModel(
        CorrelationMessage source,
        IReadOnlyList<ReplayDestination> destinations,
        IMessageReplayService replayService,
        Func<IServiceBusOperations?> getOperations)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destinations);
        ArgumentNullException.ThrowIfNull(replayService);
        ArgumentNullException.ThrowIfNull(getOperations);
        if (destinations.Count == 0)
        {
            throw new ArgumentException("At least one replay destination is required", nameof(destinations));
        }

        Source = source;
        AvailableDestinations = destinations;
        _replayService = replayService;
        _getOperations = getOperations;
        SelectedDestination = destinations[0];
        _initialRequest = replayService.CreateRequest(source, SelectedDestination);
        _sourceProperties = source.Properties;

        _body = _initialRequest.Body;
        _contentType = _initialRequest.ContentType;
        _correlationId = _initialRequest.CorrelationId;
        _messageId = _initialRequest.MessageId;
        _sessionId = _initialRequest.SessionId;
        _subject = _initialRequest.Subject;
        _to = _initialRequest.To;
        _replyTo = _initialRequest.ReplyTo;
        _replyToSessionId = _initialRequest.ReplyToSessionId;
        _partitionKey = _initialRequest.PartitionKey;
        _timeToLiveText = _initialRequest.TimeToLive?.ToString();
        _rateLimitPerSecond = _initialRequest.RateLimitPerSecond;

        foreach (var property in source.Properties)
        {
            CustomProperties.Add(new CustomProperty
            {
                Key = property.Key,
                Value = property.Value?.ToString() ?? string.Empty
            });
        }
    }

    partial void OnSelectedDestinationChanged(ReplayDestination? value)
    {
        OnPropertyChanged(nameof(IsProductionDestination));
        HasPreview = false;
    }

    [RelayCommand]
    private void AddCustomProperty()
    {
        CustomProperties.Add(new CustomProperty());
        HasPreview = false;
    }

    [RelayCommand]
    private void RemoveCustomProperty(CustomProperty property)
    {
        CustomProperties.Remove(property);
        HasPreview = false;
    }

    [RelayCommand]
    private void BuildPreview()
    {
        ErrorMessage = null;
        ResultMessage = null;
        if (!TryBuildRequest(out var request, out var error))
        {
            ErrorMessage = error;
            Preview = new ReplayPreview([error!], [], []);
            HasPreview = false;
            return;
        }

        Preview = _replayService.Preview(request!);
        HasPreview = true;
    }

    [RelayCommand]
    private async Task ReplayAsync(CancellationToken ct = default)
    {
        if (!HasPreview)
        {
            ErrorMessage = "Preview the replay request before sending";
            return;
        }

        var operations = _getOperations();
        if (operations == null)
        {
            ErrorMessage = "No active connection is available for replay";
            return;
        }

        if (!TryBuildRequest(out var request, out var error))
        {
            ErrorMessage = error;
            return;
        }

        IsSending = true;
        ErrorMessage = null;
        ResultMessage = null;
        try
        {
            var result = await _replayService.ReplayAsync(operations, request!, ct);
            ResultMessage = result.Message;
            AuditWarning = result.AuditWarning;
            if (!result.IsSuccess)
            {
                ErrorMessage = result.ValidationErrors is { Count: > 0 }
                    ? string.Join(Environment.NewLine, result.ValidationErrors)
                    : result.Message;
            }
        }
        finally
        {
            IsSending = false;
        }
    }

    private bool TryBuildRequest(out ReplayRequest? request, out string? error)
    {
        request = null;
        error = null;
        if (SelectedDestination == null)
        {
            error = "Select a replay destination";
            return false;
        }

        DateTimeOffset? scheduledAt = null;
        if (!string.IsNullOrWhiteSpace(ScheduledEnqueueTimeText))
        {
            if (!DateTimeOffset.TryParse(ScheduledEnqueueTimeText, out var parsed))
            {
                error = "Scheduled enqueue time must be a valid ISO 8601 timestamp";
                return false;
            }

            scheduledAt = parsed;
        }

        TimeSpan? timeToLive = null;
        if (!string.IsNullOrWhiteSpace(TimeToLiveText))
        {
            if (!TimeSpan.TryParse(TimeToLiveText, out var parsed))
            {
                error = "Time to live must be a valid time span";
                return false;
            }

            timeToLive = parsed;
        }

        request = _initialRequest with
        {
            Destination = SelectedDestination,
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
            TimeToLive = timeToLive,
            ScheduledEnqueueTime = scheduledAt,
            RateLimitPerSecond = RateLimitPerSecond,
            Properties = BuildProperties(),
            IsConfirmed = IsConfirmed,
            IsProductionAcknowledged = IsProductionAcknowledged
        };
        return true;
    }

    private IReadOnlyDictionary<string, object> BuildProperties()
    {
        var properties = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var property in CustomProperties)
        {
            if (_sourceProperties.TryGetValue(property.Key, out var original) &&
                string.Equals(original?.ToString(), property.Value, StringComparison.Ordinal))
            {
                properties[property.Key] = original ?? string.Empty;
            }
            else
            {
                properties[property.Key] = property.Value;
            }
        }

        return properties;
    }
}
