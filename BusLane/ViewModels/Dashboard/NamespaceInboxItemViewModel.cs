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
