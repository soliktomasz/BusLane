namespace BusLane.ViewModels.Dashboard;

using BusLane.Models;
using BusLane.ViewModels.Core;
using CommunityToolkit.Mvvm.Input;

/// <summary>
/// Represents a single ranked inbox row and its quick actions.
/// </summary>
public partial class NamespaceInboxItemViewModel : ViewModelBase
{
    private readonly Action<NamespaceInboxItem> _openMessages;
    private readonly Action<NamespaceInboxItem> _openDeadLetter;
    private readonly Action<NamespaceInboxItem> _openSessionInspector;
    private readonly Action<NamespaceInboxItem> _markReviewed;

    public NamespaceInboxItem Item { get; }

    public string EntityName => Item.EntityName;
    public BusLane.Models.Dashboard.EntityType EntityType => Item.EntityType;
    public string? TopicName => Item.TopicName;
    public bool RequiresSession => Item.RequiresSession;
    public double Score => Item.Score;
    public IReadOnlyList<string> Reasons => Item.Reasons;
    public string ReasonSummary => string.Join(" • ", Item.Reasons);
    public long ActiveMessageCount => Item.ActiveMessageCount;
    public long DeadLetterCount => Item.DeadLetterCount;
    public long ScheduledCount => Item.ScheduledCount;
    public int ActiveAlertCount => Item.ActiveAlertCount;
    public bool HasScheduledMessages => Item.ScheduledCount > 0;
    public bool HasActiveAlerts => Item.ActiveAlertCount > 0;
    public bool CanOpenSessionInspector => Item.RequiresSession;
    public long ActiveMessageDelta { get; }
    public long DeadLetterDelta { get; }
    public long ScheduledDelta { get; }
    public int AlertDelta { get; }

    /// <summary>
    /// Triage health driving the status dot and pill: a dead-letter accumulation takes
    /// priority over a scheduled backlog; otherwise the entity is healthy.
    /// </summary>
    public BusLane.Models.Dashboard.InboxStatus Status => DeadLetterCount > 0
        ? BusLane.Models.Dashboard.InboxStatus.Critical
        : ScheduledCount > 0
            ? BusLane.Models.Dashboard.InboxStatus.Warning
            : BusLane.Models.Dashboard.InboxStatus.Healthy;

    /// <summary>Short label shown in the status pill.</summary>
    public string StatusLabel => Status switch
    {
        BusLane.Models.Dashboard.InboxStatus.Critical => "DLQ",
        BusLane.Models.Dashboard.InboxStatus.Warning => "Scheduled",
        _ => "OK"
    };

    public NamespaceInboxItemViewModel(
        NamespaceInboxItem item,
        long activeMessageDelta,
        long deadLetterDelta,
        long scheduledDelta,
        int alertDelta,
        Action<NamespaceInboxItem> openMessages,
        Action<NamespaceInboxItem> openDeadLetter,
        Action<NamespaceInboxItem> openSessionInspector,
        Action<NamespaceInboxItem> markReviewed)
    {
        Item = item;
        ActiveMessageDelta = activeMessageDelta;
        DeadLetterDelta = deadLetterDelta;
        ScheduledDelta = scheduledDelta;
        AlertDelta = alertDelta;
        _openMessages = openMessages;
        _openDeadLetter = openDeadLetter;
        _openSessionInspector = openSessionInspector;
        _markReviewed = markReviewed;
    }

    [RelayCommand]
    private void OpenMessages()
    {
        _openMessages(Item);
    }

    [RelayCommand]
    private void OpenDeadLetter()
    {
        _openDeadLetter(Item);
    }

    [RelayCommand]
    private void OpenSessionInspector()
    {
        _openSessionInspector(Item);
    }

    [RelayCommand]
    private void MarkReviewed()
    {
        _markReviewed(Item);
    }
}
