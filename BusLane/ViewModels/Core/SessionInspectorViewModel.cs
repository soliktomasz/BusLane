namespace BusLane.ViewModels.Core;

using System.Collections.ObjectModel;
using BusLane.Models;
using BusLane.Models.Logging;
using BusLane.Services.ServiceBus;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

/// <summary>
/// Inspects discovered sessions for the currently selected session-enabled entity.
/// </summary>
public partial class SessionInspectorViewModel : ViewModelBase
{
    private readonly Func<IServiceBusOperations?> _getOperations;
    private readonly MessageOperationsViewModel _messageOperations;
    private readonly ILogSink _logSink;
    private readonly Func<string?> _getEntityName;
    private readonly Func<string?> _getSubscriptionName;
    private readonly Func<bool> _getRequiresSession;
    private readonly Action<int> _setSelectedMessageTabIndex;
    private readonly Action<string> _setStatus;

    [ObservableProperty] private bool _isLoadingSessions;
    [ObservableProperty] private SessionInspectorItem? _selectedSession;

    public ObservableCollection<SessionInspectorItem> Sessions { get; } = [];

    public SessionInspectorViewModel(
        Func<IServiceBusOperations?> getOperations,
        MessageOperationsViewModel messageOperations,
        ILogSink logSink,
        Func<string?> getEntityName,
        Func<string?> getSubscriptionName,
        Func<bool> getRequiresSession,
        Action<int> setSelectedMessageTabIndex,
        Action<string> setStatus)
    {
        _getOperations = getOperations;
        _messageOperations = messageOperations;
        _logSink = logSink;
        _getEntityName = getEntityName;
        _getSubscriptionName = getSubscriptionName;
        _getRequiresSession = getRequiresSession;
        _setSelectedMessageTabIndex = setSelectedMessageTabIndex;
        _setStatus = setStatus;
    }

    [RelayCommand]
    public async Task LoadSessionsAsync()
    {
        if (IsLoadingSessions)
        {
            return;
        }

        var entityName = _getEntityName();
        var operations = _getOperations();
        if (operations == null || string.IsNullOrWhiteSpace(entityName) || !_getRequiresSession())
        {
            Clear();
            return;
        }

        IsLoadingSessions = true;
        _setStatus("Loading sessions...");

        try
        {
            var items = await operations.GetSessionInspectorItemsAsync(entityName, _getSubscriptionName());

            Sessions.Clear();
            foreach (var item in items)
            {
                Sessions.Add(item);
            }

            _setStatus(items.Count == 0 ? "No sessions discovered" : $"Loaded {items.Count} session(s)");
        }
        catch (Exception ex)
        {
            _setStatus($"Error loading sessions: {ex.Message}");
            _logSink.Log(new LogEntry(
                DateTime.UtcNow,
                LogSource.ServiceBus,
                LogLevel.Error,
                "Failed to load session inspector data",
                ex.Message));
        }
        finally
        {
            IsLoadingSessions = false;
        }
    }

    [RelayCommand]
    public async Task OpenSessionMessagesAsync(SessionInspectorItem session)
    {
        SelectedSession = session;
        _messageOperations.OpenSessionScope(session.SessionId, session.ActiveMessageCount, session.DeadLetterMessageCount);
        _setSelectedMessageTabIndex(0);
        await _messageOperations.LoadMessagesAsync();
    }

    public void Clear()
    {
        Sessions.Clear();
        SelectedSession = null;
    }
}
