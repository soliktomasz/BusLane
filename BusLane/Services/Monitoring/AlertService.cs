namespace BusLane.Services.Monitoring;

using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;
using BusLane.Models;
using BusLane.Services.Infrastructure;
using Serilog;
using static BusLane.Services.Infrastructure.SafeJsonSerializer;

public class AlertService : IAlertService
{
    // Cache for compiled regex patterns to avoid recompilation on every match
    private static readonly ConcurrentDictionary<string, Regex?> RegexCache = new();

    // Cached JsonSerializerOptions to avoid recreation on every serialization
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    // Maximum time allowed for regex pattern matching to prevent catastrophic backtracking
    private static readonly TimeSpan RegexMatchTimeout = TimeSpan.FromMilliseconds(100);

    // Lock object for thread-safe access to mutable collections
    private readonly object _lock = new();

    private readonly List<AlertRule> _rules = [];
    private readonly List<AlertEvent> _activeAlerts = [];
    private readonly List<AlertHistoryEntry> _history = [];
    private readonly HashSet<string> _triggeredAlertKeys = []; // To prevent duplicate alerts
    private readonly Dictionary<string, DateTimeOffset> _lastTriggeredAtByKey = new();
    private readonly IEnumerable<INotificationChannel> _notificationChannels;
    private readonly Func<DateTimeOffset> _nowProvider;
    private readonly string _rulesPath;
    private readonly string _historyPath;

    public IReadOnlyList<AlertRule> Rules
    {
        get
        {
            lock (_lock)
            {
                return _rules.ToList().AsReadOnly();
            }
        }
    }

    public IReadOnlyList<AlertEvent> ActiveAlerts
    {
        get
        {
            lock (_lock)
            {
                return _activeAlerts.ToList().AsReadOnly();
            }
        }
    }

    public IReadOnlyList<AlertHistoryEntry> History
    {
        get
        {
            lock (_lock)
            {
                return _history.ToList().AsReadOnly();
            }
        }
    }

    public event EventHandler<AlertEvent>? AlertTriggered;
    public event EventHandler? AlertsChanged;

    public AlertService(
        string? rulesPath = null,
        string? historyPath = null,
        IEnumerable<INotificationChannel>? notificationChannels = null,
        Func<DateTimeOffset>? nowProvider = null)
    {
        _rulesPath = rulesPath ?? AppPaths.AlertRules;
        _historyPath = historyPath ?? AppPaths.AlertHistory;
        _notificationChannels = notificationChannels ?? [];
        _nowProvider = nowProvider ?? (() => DateTimeOffset.UtcNow);
        LoadRules();
        LoadHistory();
        // No default rules - let users create their own when needed (YAGNI principle)
    }

    public void AddRule(AlertRule rule)
    {
        lock (_lock)
        {
            _rules.Add(rule);
            SaveRulesInternal();
        }
        AlertsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void RemoveRule(string ruleId)
    {
        int removedCount;
        lock (_lock)
        {
            removedCount = _rules.RemoveAll(r => r.Id == ruleId);
            SaveRulesInternal();
        }
        AlertsChanged?.Invoke(this, EventArgs.Empty);
        if (removedCount > 0)
        {
            Log.Information("Alert rule removed: {RuleId}", ruleId);
        }
    }

    public void UpdateRule(AlertRule rule)
    {
        bool updated = false;
        lock (_lock)
        {
            var index = _rules.FindIndex(r => r.Id == rule.Id);
            if (index >= 0)
            {
                _rules[index] = rule;
                SaveRulesInternal();
                updated = true;
            }
        }
        if (updated)
        {
            AlertsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void SetRuleEnabled(string ruleId, bool enabled)
    {
        AlertRule? rule;
        lock (_lock)
        {
            rule = _rules.Find(r => r.Id == ruleId);
        }
        if (rule != null)
        {
            var updatedRule = rule with { IsEnabled = enabled };
            UpdateRule(updatedRule);
        }
    }

    public Task<IEnumerable<AlertEvent>> EvaluateAlertsAsync(
        IEnumerable<QueueInfo> queues,
        IEnumerable<SubscriptionInfo> subscriptions)
    {
        var newAlerts = new List<AlertEvent>();

        // Take a snapshot of enabled rules to avoid holding lock during evaluation
        List<AlertRule> enabledRules;
        lock (_lock)
        {
            enabledRules = _rules.Where(r => r.IsEnabled).ToList();
        }

        foreach (var rule in enabledRules)
        {
            // Check queues
            foreach (var queue in queues)
            {
                if (!MatchesEntityPattern(queue.Name, rule.EntityPattern))
                    continue;

                var evaluation = EvaluateRule(rule, queue.Name, "Queue", queue);
                if (evaluation.SuppressHistory != null)
                {
                    RecordHistory(evaluation.SuppressHistory);
                }

                if (evaluation.Alert != null)
                {
                    newAlerts.Add(evaluation.Alert);
                }
            }

            // Check subscriptions
            foreach (var sub in subscriptions)
            {
                var entityName = $"{sub.TopicName}/{sub.Name}";
                if (!MatchesEntityPattern(entityName, rule.EntityPattern))
                    continue;

                var evaluation = EvaluateRuleForSubscription(rule, entityName, sub);
                if (evaluation.SuppressHistory != null)
                {
                    RecordHistory(evaluation.SuppressHistory);
                }

                if (evaluation.Alert != null)
                {
                    newAlerts.Add(evaluation.Alert);
                }
            }
        }

        // Add new alerts and trigger events (with lock for collection access)
        var triggeredAlerts = new List<AlertEvent>();
        var alertsToFire = new List<AlertEvent>();

        lock (_lock)
        {
            foreach (var alert in newAlerts)
            {
                var key = GetAlertKey(alert);
                if (!_triggeredAlertKeys.Contains(key))
                {
                    _triggeredAlertKeys.Add(key);
                    _activeAlerts.Add(alert);
                    _lastTriggeredAtByKey[key] = _nowProvider();
                    triggeredAlerts.Add(alert);
                    alertsToFire.Add(alert);
                    _history.Add(new AlertHistoryEntry(
                        Guid.NewGuid().ToString(),
                        alert.Rule.Id,
                        alert.Rule.Name,
                        alert.EntityName,
                        alert.EntityType,
                        alert.CurrentValue,
                        _nowProvider(),
                        AlertHistoryStatus.Triggered));
                }
            }
            SaveHistoryInternal();
        }

        // Fire events outside lock to avoid potential deadlocks
        foreach (var alert in alertsToFire)
        {
            AlertTriggered?.Invoke(this, alert);
            Log.Warning("Alert triggered: {AlertType} on {EntityName} - Current value: {CurrentValue}, Threshold: {Threshold}",
                alert.Rule.Type, alert.EntityName, alert.CurrentValue, alert.Rule.Threshold);
            _ = DispatchNotificationsAsync(alert);
        }

        if (triggeredAlerts.Count > 0)
        {
            AlertsChanged?.Invoke(this, EventArgs.Empty);
            Log.Information("Alert evaluation completed: {TriggeredCount} new alerts triggered", triggeredAlerts.Count);
        }

        return Task.FromResult<IEnumerable<AlertEvent>>(triggeredAlerts);
    }

    private AlertEvaluationResult EvaluateRule(AlertRule rule, string entityName, string entityType, QueueInfo queue)
    {
        double currentValue = rule.Type switch
        {
            AlertType.DeadLetterThreshold => queue.DeadLetterCount,
            AlertType.MessageCountThreshold => queue.ActiveMessageCount,
            AlertType.QueueSizeThreshold => queue.SizeInBytes,
            _ => 0
        };

        return BuildEvaluationResult(rule, entityName, entityType, currentValue);
    }

    private AlertEvaluationResult EvaluateRuleForSubscription(AlertRule rule, string entityName, SubscriptionInfo sub)
    {
        double currentValue = rule.Type switch
        {
            AlertType.DeadLetterThreshold => sub.DeadLetterCount,
            AlertType.MessageCountThreshold => sub.ActiveMessageCount,
            _ => 0
        };

        return BuildEvaluationResult(rule, entityName, "Subscription", currentValue);
    }

    private AlertEvaluationResult BuildEvaluationResult(AlertRule rule, string entityName, string entityType, double currentValue)
    {
        if (currentValue < rule.Threshold)
        {
            return new AlertEvaluationResult(null, null);
        }

        var now = _nowProvider();
        var alertKey = $"{rule.Id}:{entityName}:{rule.Type}";

        if (rule.QuietHours != null && rule.QuietHours.Contains(now.Hour))
        {
            return new AlertEvaluationResult(
                null,
                new AlertHistoryEntry(
                    Guid.NewGuid().ToString(),
                    rule.Id,
                    rule.Name,
                    entityName,
                    entityType,
                    currentValue,
                    now,
                    AlertHistoryStatus.Suppressed,
                    AlertSuppressionReason.QuietHours));
        }

        if (rule.Cooldown.HasValue &&
            _lastTriggeredAtByKey.TryGetValue(alertKey, out var lastTriggeredAt) &&
            now - lastTriggeredAt < rule.Cooldown.Value)
        {
            return new AlertEvaluationResult(
                null,
                new AlertHistoryEntry(
                    Guid.NewGuid().ToString(),
                    rule.Id,
                    rule.Name,
                    entityName,
                    entityType,
                    currentValue,
                    now,
                    AlertHistoryStatus.Suppressed,
                    AlertSuppressionReason.Cooldown));
        }

        return new AlertEvaluationResult(
            new AlertEvent(
                Guid.NewGuid().ToString(),
                rule,
                entityName,
                entityType,
                currentValue,
                now),
            null);
    }

    private static bool MatchesEntityPattern(string entityName, string? pattern)
    {
        if (string.IsNullOrEmpty(pattern))
            return true;

        // Get or create cached compiled regex for this pattern
        var regex = RegexCache.GetOrAdd(pattern, p =>
        {
            try
            {
                return new Regex(p, RegexOptions.IgnoreCase | RegexOptions.Compiled, RegexMatchTimeout);
            }
            catch (ArgumentException ex)
            {
                Log.Debug(ex, "Invalid regex pattern '{Pattern}', will use substring match", p);
                return null; // null indicates invalid pattern, use substring match
            }
        });

        // Use compiled regex if valid, otherwise fall back to substring match
        return regex?.IsMatch(entityName) ?? entityName.Contains(pattern, StringComparison.OrdinalIgnoreCase);
    }

    private static string GetAlertKey(AlertEvent alert)
    {
        return $"{alert.Rule.Id}:{alert.EntityName}:{alert.Rule.Type}";
    }

    public void AcknowledgeAlert(string alertId)
    {
        bool acknowledged = false;
        lock (_lock)
        {
            var index = _activeAlerts.FindIndex(a => a.Id == alertId);
            if (index >= 0)
            {
                var alert = _activeAlerts[index];
                _activeAlerts[index] = alert with { IsAcknowledged = true };

                // Remove from triggered keys so it can trigger again later
                var key = GetAlertKey(alert);
                _triggeredAlertKeys.Remove(key);
                acknowledged = true;
                _history.Add(new AlertHistoryEntry(
                    Guid.NewGuid().ToString(),
                    alert.Rule.Id,
                    alert.Rule.Name,
                    alert.EntityName,
                    alert.EntityType,
                    alert.CurrentValue,
                    _nowProvider(),
                    AlertHistoryStatus.Acknowledged));
                SaveHistoryInternal();
            }
        }

        if (acknowledged)
        {
            AlertsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void ClearAcknowledgedAlerts()
    {
        lock (_lock)
        {
            _activeAlerts.RemoveAll(a => a.IsAcknowledged);
        }
        AlertsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void TestRule(AlertRule rule)
    {
        var testAlert = new AlertEvent(
            Guid.NewGuid().ToString(),
            rule,
            "[Test Entity]",
            "Test",
            rule.Threshold,
            DateTimeOffset.UtcNow
        );

        lock (_lock)
        {
            _activeAlerts.Add(testAlert);
            _history.Add(new AlertHistoryEntry(
                Guid.NewGuid().ToString(),
                rule.Id,
                rule.Name,
                "[Test Entity]",
                "Test",
                rule.Threshold,
                _nowProvider(),
                AlertHistoryStatus.Triggered));
            SaveHistoryInternal();
        }
        AlertTriggered?.Invoke(this, testAlert);
        AlertsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SaveRules()
    {
        lock (_lock)
        {
            SaveRulesInternal();
        }
    }

    /// <summary>
    /// Internal save method - must be called while holding the lock.
    /// </summary>
    private void SaveRulesInternal()
    {
        try
        {
            AppPaths.EnsureDirectoryExists();

            var data = _rules.Select(r => new AlertRuleData
            {
                Id = r.Id,
                Name = r.Name,
                Type = r.Type.ToString(),
                Severity = r.Severity.ToString(),
                Threshold = r.Threshold,
                IsEnabled = r.IsEnabled,
                EntityPattern = r.EntityPattern,
                CooldownMinutes = r.Cooldown?.TotalMinutes,
                QuietHoursStartHour = r.QuietHours?.StartHour,
                QuietHoursEndHour = r.QuietHours?.EndHour,
                DeliveryTargets = r.DeliveryTargets?.ToList()
            }).ToList();

            var json = JsonSerializer.Serialize(data, JsonOptions);
            AppPaths.CreateSecureFile(_rulesPath, json);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to save alert rules to {Path}", _rulesPath);
        }
    }

    public void LoadRules()
    {
        lock (_lock)
        {
            try
            {
                if (File.Exists(_rulesPath))
                {
                    var json = File.ReadAllText(_rulesPath);
                    var data = Deserialize<List<AlertRuleData>>(json);

                    if (data != null)
                    {
                        _rules.Clear();
                        foreach (var item in data)
                        {
                            if (Enum.TryParse<AlertType>(item.Type, out var type) &&
                                Enum.TryParse<AlertSeverity>(item.Severity, out var severity))
                            {
                                _rules.Add(new AlertRule(
                                    item.Id,
                                    item.Name,
                                    type,
                                    severity,
                                    item.Threshold,
                                    item.IsEnabled,
                                    item.EntityPattern,
                                    item.CooldownMinutes.HasValue ? TimeSpan.FromMinutes(item.CooldownMinutes.Value) : null,
                                    item.QuietHoursStartHour.HasValue && item.QuietHoursEndHour.HasValue
                                        ? new QuietHoursWindow(item.QuietHoursStartHour.Value, item.QuietHoursEndHour.Value)
                                        : null,
                                    item.DeliveryTargets
                                ));
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Failed to load alert rules from {Path}, using defaults", _rulesPath);
            }
        }
    }

    private async Task DispatchNotificationsAsync(AlertEvent alert)
    {
        if (alert.Rule.DeliveryTargets == null || alert.Rule.DeliveryTargets.Count == 0)
        {
            return;
        }

        foreach (var target in alert.Rule.DeliveryTargets)
        {
            var channel = _notificationChannels.FirstOrDefault(c => c.ChannelType == target.ChannelType);
            if (channel == null)
            {
                continue;
            }

            var historyEntry = await channel.SendAsync(alert, target);
            RecordHistory(historyEntry);
        }
    }

    private void RecordHistory(AlertHistoryEntry historyEntry)
    {
        lock (_lock)
        {
            _history.Add(historyEntry);
            SaveHistoryInternal();
        }
    }

    private void LoadHistory()
    {
        lock (_lock)
        {
            try
            {
                if (!File.Exists(_historyPath))
                {
                    return;
                }

                var json = File.ReadAllText(_historyPath);
                var data = Deserialize<List<AlertHistoryEntry>>(json);
                if (data != null)
                {
                    _history.Clear();
                    _history.AddRange(data);
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Failed to load alert history from {Path}", _historyPath);
            }
        }
    }

    private void SaveHistoryInternal()
    {
        try
        {
            AppPaths.EnsureDirectoryExists();
            var json = JsonSerializer.Serialize(_history, JsonOptions);
            AppPaths.CreateSecureFile(_historyPath, json);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Failed to save alert history to {Path}", _historyPath);
        }
    }

    private class AlertRuleData
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Type { get; set; } = "";
        public string Severity { get; set; } = "";
        public double Threshold { get; set; }
        public bool IsEnabled { get; set; }
        public string? EntityPattern { get; set; }
        public double? CooldownMinutes { get; set; }
        public int? QuietHoursStartHour { get; set; }
        public int? QuietHoursEndHour { get; set; }
        public List<AlertDeliveryTarget>? DeliveryTargets { get; set; }
    }

    private sealed record AlertEvaluationResult(AlertEvent? Alert, AlertHistoryEntry? SuppressHistory);
}
