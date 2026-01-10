using System.Collections.ObjectModel;
using BusLane.Models;
using BusLane.ViewModels.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BusLane.ViewModels;

using Services.Monitoring;

public partial class AlertsViewModel : ViewModelBase
{
    private readonly IAlertService _alertService;
    private readonly INotificationService _notificationService;
    private readonly Action _onClose;

    [ObservableProperty] private bool _isAddingRule;
    [ObservableProperty] private bool _isEditingRule;
    [ObservableProperty] private AlertRule? _editingRule;
    [ObservableProperty] private bool _systemNotificationsEnabled;

    // Tab navigation
    [ObservableProperty] private bool _isActiveAlertsTabSelected = true;
    [ObservableProperty] private bool _isRulesTabSelected;

    // New/Edit rule form
    [ObservableProperty] private string _ruleName = "";
    [ObservableProperty] private AlertType _selectedAlertType = AlertType.DeadLetterThreshold;
    [ObservableProperty] private AlertSeverity _selectedSeverity = AlertSeverity.Warning;
    [ObservableProperty] private double _threshold = 10;
    [ObservableProperty] private string _entityPattern = "";
    [ObservableProperty] private bool _ruleEnabled = true;

    public ObservableCollection<AlertRule> Rules { get; } = [];
    public ObservableCollection<AlertEvent> ActiveAlerts { get; } = [];

    public AlertType[] AvailableAlertTypes { get; } = Enum.GetValues<AlertType>();
    public AlertSeverity[] AvailableSeverities { get; } = Enum.GetValues<AlertSeverity>();

    public int UnacknowledgedCount => ActiveAlerts.Count(a => !a.IsAcknowledged);
    public bool HasAlerts => ActiveAlerts.Count > 0;

    public AlertsViewModel(IAlertService alertService, INotificationService notificationService, Action onClose)
    {
        _alertService = alertService;
        _notificationService = notificationService;
        _onClose = onClose;
        _systemNotificationsEnabled = _notificationService.IsEnabled;
        
        // Single event handler - AlertsChanged covers all cases including new alerts
        _alertService.AlertsChanged += OnAlertsChanged;

        LoadData();
    }

    partial void OnSystemNotificationsEnabledChanged(bool value)
    {
        _notificationService.IsEnabled = value;
    }

    private void LoadData()
    {
        Rules.Clear();
        foreach (var rule in _alertService.Rules)
        {
            Rules.Add(rule);
        }

        ActiveAlerts.Clear();
        foreach (var alert in _alertService.ActiveAlerts)
        {
            ActiveAlerts.Add(alert);
        }

        OnPropertyChanged(nameof(UnacknowledgedCount));
        OnPropertyChanged(nameof(HasAlerts));
    }

    private void OnAlertsChanged(object? sender, EventArgs e)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(LoadData);
    }


    public string FormTitle => IsEditingRule ? "Edit Alert Rule" : "New Alert Rule";

    [RelayCommand]
    private void ShowAddRule()
    {
        ResetForm();
        IsAddingRule = true;
        IsEditingRule = false;
        IsActiveAlertsTabSelected = false;
        IsRulesTabSelected = true;
        OnPropertyChanged(nameof(FormTitle));
    }

    [RelayCommand]
    private void ShowEditRule(AlertRule rule)
    {
        EditingRule = rule;
        RuleName = rule.Name;
        SelectedAlertType = rule.Type;
        SelectedSeverity = rule.Severity;
        Threshold = rule.Threshold;
        EntityPattern = rule.EntityPattern ?? "";
        RuleEnabled = rule.IsEnabled;

        IsAddingRule = true; // Show the form
        IsEditingRule = true;
        OnPropertyChanged(nameof(FormTitle));
    }

    [RelayCommand]
    private void SaveRule()
    {
        if (string.IsNullOrWhiteSpace(RuleName))
            return;

        var rule = new AlertRule(
            IsEditingRule && EditingRule != null ? EditingRule.Id : Guid.NewGuid().ToString(),
            RuleName,
            SelectedAlertType,
            SelectedSeverity,
            Threshold,
            RuleEnabled,
            string.IsNullOrWhiteSpace(EntityPattern) ? null : EntityPattern
        );

        if (IsEditingRule)
        {
            _alertService.UpdateRule(rule);
        }
        else
        {
            _alertService.AddRule(rule);
        }

        CancelEdit();
        LoadData();
    }

    [RelayCommand]
    private void CancelEdit()
    {
        IsAddingRule = false;
        IsEditingRule = false;
        EditingRule = null;
        ResetForm();
    }

    [RelayCommand]
    private void DeleteRule(AlertRule rule)
    {
        _alertService.RemoveRule(rule.Id);
        LoadData();
    }

    [RelayCommand]
    private void ToggleRuleEnabled(AlertRule rule)
    {
        _alertService.SetRuleEnabled(rule.Id, !rule.IsEnabled);
        LoadData();
    }

    [RelayCommand]
    private void TestRule(AlertRule rule)
    {
        _alertService.TestRule(rule);
    }

    [RelayCommand]
    private void AcknowledgeAlert(AlertEvent alert)
    {
        _alertService.AcknowledgeAlert(alert.Id);
    }

    [RelayCommand]
    private void AcknowledgeAllAlerts()
    {
        foreach (var alert in ActiveAlerts.Where(a => !a.IsAcknowledged).ToList())
        {
            _alertService.AcknowledgeAlert(alert.Id);
        }
    }

    [RelayCommand]
    private void ClearAcknowledgedAlerts()
    {
        _alertService.ClearAcknowledgedAlerts();
    }

    [RelayCommand]
    private void Close()
    {
        _onClose();
    }

    private void ResetForm()
    {
        RuleName = "";
        SelectedAlertType = AlertType.DeadLetterThreshold;
        SelectedSeverity = AlertSeverity.Warning;
        Threshold = 10;
        EntityPattern = "";
        RuleEnabled = true;
    }

    public static string GetAlertTypeDisplayName(AlertType type) => type switch
    {
        AlertType.DeadLetterThreshold => "Dead Letter Count",
        AlertType.MessageCountThreshold => "Message Count",
        AlertType.QueueSizeThreshold => "Queue Size (bytes)",
        _ => type.ToString()
    };

    public static string GetSeverityColor(AlertSeverity severity) => severity switch
    {
        AlertSeverity.Info => "#0078D4",
        AlertSeverity.Warning => "#FF8C00",
        AlertSeverity.Critical => "#D13438",
        _ => "#666666"
    };
}

