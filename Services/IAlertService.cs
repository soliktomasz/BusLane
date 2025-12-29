using BusLane.Models;

namespace BusLane.Services;

public interface IAlertService
{
    /// <summary>
    /// Get all configured alert rules
    /// </summary>
    IReadOnlyList<AlertRule> Rules { get; }

    /// <summary>
    /// Get all active (unacknowledged) alerts
    /// </summary>
    IReadOnlyList<AlertEvent> ActiveAlerts { get; }

    /// <summary>
    /// Add a new alert rule
    /// </summary>
    void AddRule(AlertRule rule);

    /// <summary>
    /// Remove an alert rule
    /// </summary>
    void RemoveRule(string ruleId);

    /// <summary>
    /// Update an existing alert rule
    /// </summary>
    void UpdateRule(AlertRule rule);

    /// <summary>
    /// Enable or disable a rule
    /// </summary>
    void SetRuleEnabled(string ruleId, bool enabled);

    /// <summary>
    /// Evaluate alerts against current queue/subscription metrics
    /// </summary>
    Task<IEnumerable<AlertEvent>> EvaluateAlertsAsync(
        IEnumerable<QueueInfo> queues,
        IEnumerable<SubscriptionInfo> subscriptions);

    /// <summary>
    /// Acknowledge an alert
    /// </summary>
    void AcknowledgeAlert(string alertId);

    /// <summary>
    /// Clear all acknowledged alerts
    /// </summary>
    void ClearAcknowledgedAlerts();

    /// <summary>
    /// Event raised when a new alert is triggered
    /// </summary>
    event EventHandler<AlertEvent>? AlertTriggered;

    /// <summary>
    /// Event raised when alerts are cleared or acknowledged
    /// </summary>
    event EventHandler? AlertsChanged;

    /// <summary>
    /// Save rules to storage
    /// </summary>
    void SaveRules();

    /// <summary>
    /// Load rules from storage
    /// </summary>
    void LoadRules();

    /// <summary>
    /// Trigger a test alert for the specified rule
    /// </summary>
    void TestRule(AlertRule rule);
}

