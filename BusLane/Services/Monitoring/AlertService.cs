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
    private readonly HashSet<string> _triggeredAlertKeys = []; // To prevent duplicate alerts

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

    public event EventHandler<AlertEvent>? AlertTriggered;
    public event EventHandler? AlertsChanged;

    public AlertService()
    {
        LoadRules();
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

                var alertEvent = EvaluateRule(rule, queue.Name, "Queue", queue);
                if (alertEvent != null)
                {
                    newAlerts.Add(alertEvent);
                }
            }

            // Check subscriptions
            foreach (var sub in subscriptions)
            {
                var entityName = $"{sub.TopicName}/{sub.Name}";
                if (!MatchesEntityPattern(entityName, rule.EntityPattern))
                    continue;

                var alertEvent = EvaluateRuleForSubscription(rule, entityName, sub);
                if (alertEvent != null)
                {
                    newAlerts.Add(alertEvent);
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
                    triggeredAlerts.Add(alert);
                    alertsToFire.Add(alert);
                }
            }
        }

        // Fire events outside lock to avoid potential deadlocks
        foreach (var alert in alertsToFire)
        {
            AlertTriggered?.Invoke(this, alert);
            Log.Warning("Alert triggered: {AlertType} on {EntityName} - Current value: {CurrentValue}, Threshold: {Threshold}",
                alert.Rule.Type, alert.EntityName, alert.CurrentValue, alert.Rule.Threshold);
        }

        if (triggeredAlerts.Count > 0)
        {
            AlertsChanged?.Invoke(this, EventArgs.Empty);
            Log.Information("Alert evaluation completed: {TriggeredCount} new alerts triggered", triggeredAlerts.Count);
        }

        return Task.FromResult<IEnumerable<AlertEvent>>(triggeredAlerts);
    }

    private AlertEvent? EvaluateRule(AlertRule rule, string entityName, string entityType, QueueInfo queue)
    {
        double currentValue = rule.Type switch
        {
            AlertType.DeadLetterThreshold => queue.DeadLetterCount,
            AlertType.MessageCountThreshold => queue.ActiveMessageCount,
            AlertType.QueueSizeThreshold => queue.SizeInBytes,
            _ => 0
        };

        if (currentValue >= rule.Threshold)
        {
            return new AlertEvent(
                Guid.NewGuid().ToString(),
                rule,
                entityName,
                entityType,
                currentValue,
                DateTimeOffset.UtcNow
            );
        }

        return null;
    }

    private AlertEvent? EvaluateRuleForSubscription(AlertRule rule, string entityName, SubscriptionInfo sub)
    {
        double currentValue = rule.Type switch
        {
            AlertType.DeadLetterThreshold => sub.DeadLetterCount,
            AlertType.MessageCountThreshold => sub.ActiveMessageCount,
            _ => 0
        };

        if (currentValue >= rule.Threshold)
        {
            return new AlertEvent(
                Guid.NewGuid().ToString(),
                rule,
                entityName,
                "Subscription",
                currentValue,
                DateTimeOffset.UtcNow
            );
        }

        return null;
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
                EntityPattern = r.EntityPattern
            }).ToList();

            var json = JsonSerializer.Serialize(data, JsonOptions);
            AppPaths.CreateSecureFile(AppPaths.AlertRules, json);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to save alert rules to {Path}", AppPaths.AlertRules);
        }
    }

    public void LoadRules()
    {
        lock (_lock)
        {
            try
            {
                if (File.Exists(AppPaths.AlertRules))
                {
                    var json = File.ReadAllText(AppPaths.AlertRules);
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
                                    item.EntityPattern
                                ));
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Failed to load alert rules from {Path}, using defaults", AppPaths.AlertRules);
            }
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
    }
}

