using BusLane.Models;
using BusLane.Services.Monitoring;
using FluentAssertions;

namespace BusLane.Tests.Services.Monitoring;

public class AlertServiceTests
{
    private readonly AlertService _sut;

    public AlertServiceTests()
    {
        // Create service - note: this will try to load from file system
        // For proper isolation, we'd need to refactor to inject file path
        _sut = new AlertService();
        
        // Clear any loaded/default rules for clean tests
        foreach (var rule in _sut.Rules.ToList())
        {
            _sut.RemoveRule(rule.Id);
        }
    }

    #region Rule Management Tests

    [Fact]
    public void AddRule_WithValidRule_AddsToRulesList()
    {
        // Arrange
        var rule = CreateTestRule();

        // Act
        _sut.AddRule(rule);

        // Assert
        _sut.Rules.Should().Contain(r => r.Id == rule.Id);
    }

    [Fact]
    public void AddRule_RaisesAlertsChangedEvent()
    {
        // Arrange
        var eventRaised = false;
        _sut.AlertsChanged += (_, _) => eventRaised = true;
        var rule = CreateTestRule();

        // Act
        _sut.AddRule(rule);

        // Assert
        eventRaised.Should().BeTrue();
    }

    [Fact]
    public void RemoveRule_WithExistingRule_RemovesFromList()
    {
        // Arrange
        var rule = CreateTestRule();
        _sut.AddRule(rule);

        // Act
        _sut.RemoveRule(rule.Id);

        // Assert
        _sut.Rules.Should().NotContain(r => r.Id == rule.Id);
    }

    [Fact]
    public void UpdateRule_WithExistingRule_UpdatesRule()
    {
        // Arrange
        var rule = CreateTestRule("Original Name");
        _sut.AddRule(rule);
        var updatedRule = rule with { Name = "Updated Name" };

        // Act
        _sut.UpdateRule(updatedRule);

        // Assert
        _sut.Rules.Should().Contain(r => r.Name == "Updated Name");
        _sut.Rules.Should().NotContain(r => r.Name == "Original Name");
    }

    [Fact]
    public void SetRuleEnabled_ToggleEnabledState()
    {
        // Arrange
        var rule = CreateTestRule(isEnabled: true);
        _sut.AddRule(rule);

        // Act
        _sut.SetRuleEnabled(rule.Id, false);

        // Assert
        _sut.Rules.First(r => r.Id == rule.Id).IsEnabled.Should().BeFalse();
    }

    #endregion

    #region Alert Evaluation Tests

    [Fact]
    public async Task EvaluateAlertsAsync_WithQueueExceedingThreshold_TriggersAlert()
    {
        // Arrange
        var rule = new AlertRule(
            Guid.NewGuid().ToString(),
            "High Dead Letter",
            AlertType.DeadLetterThreshold,
            AlertSeverity.Warning,
            10, // threshold
            true
        );
        _sut.AddRule(rule);

        var queues = new[]
        {
            CreateQueueInfo("test-queue", deadLetterCount: 15)
        };

        // Act
        var alerts = await _sut.EvaluateAlertsAsync(queues, Enumerable.Empty<SubscriptionInfo>());

        // Assert
        alerts.Should().ContainSingle()
            .Which.Should().Match<AlertEvent>(a => 
                a.EntityName == "test-queue" && 
                a.CurrentValue == 15);
    }

    [Fact]
    public async Task EvaluateAlertsAsync_WithQueueBelowThreshold_DoesNotTriggerAlert()
    {
        // Arrange
        var rule = new AlertRule(
            Guid.NewGuid().ToString(),
            "High Dead Letter",
            AlertType.DeadLetterThreshold,
            AlertSeverity.Warning,
            100, // threshold
            true
        );
        _sut.AddRule(rule);

        var queues = new[]
        {
            CreateQueueInfo("test-queue", deadLetterCount: 50)
        };

        // Act
        var alerts = await _sut.EvaluateAlertsAsync(queues, Enumerable.Empty<SubscriptionInfo>());

        // Assert
        alerts.Should().BeEmpty();
    }

    [Fact]
    public async Task EvaluateAlertsAsync_WithMessageCountThreshold_EvaluatesCorrectly()
    {
        // Arrange
        var rule = new AlertRule(
            Guid.NewGuid().ToString(),
            "High Message Count",
            AlertType.MessageCountThreshold,
            AlertSeverity.Warning,
            100,
            true
        );
        _sut.AddRule(rule);

        var queues = new[]
        {
            CreateQueueInfo("test-queue", activeMessageCount: 150)
        };

        // Act
        var alerts = await _sut.EvaluateAlertsAsync(queues, Enumerable.Empty<SubscriptionInfo>());

        // Assert
        alerts.Should().ContainSingle()
            .Which.CurrentValue.Should().Be(150);
    }

    [Fact]
    public async Task EvaluateAlertsAsync_WithQueueSizeThreshold_EvaluatesCorrectly()
    {
        // Arrange
        var rule = new AlertRule(
            Guid.NewGuid().ToString(),
            "Large Queue Size",
            AlertType.QueueSizeThreshold,
            AlertSeverity.Warning,
            1000,
            true
        );
        _sut.AddRule(rule);

        var queues = new[]
        {
            CreateQueueInfo("test-queue", sizeInBytes: 2000)
        };

        // Act
        var alerts = await _sut.EvaluateAlertsAsync(queues, Enumerable.Empty<SubscriptionInfo>());

        // Assert
        alerts.Should().ContainSingle();
    }

    [Fact]
    public async Task EvaluateAlertsAsync_WithDisabledRule_DoesNotEvaluate()
    {
        // Arrange
        var rule = new AlertRule(
            Guid.NewGuid().ToString(),
            "Disabled Rule",
            AlertType.DeadLetterThreshold,
            AlertSeverity.Warning,
            10,
            IsEnabled: false
        );
        _sut.AddRule(rule);

        var queues = new[]
        {
            CreateQueueInfo("test-queue", deadLetterCount: 100)
        };

        // Act
        var alerts = await _sut.EvaluateAlertsAsync(queues, Enumerable.Empty<SubscriptionInfo>());

        // Assert
        alerts.Should().BeEmpty();
    }

    [Fact]
    public async Task EvaluateAlertsAsync_WithSubscription_TriggersAlert()
    {
        // Arrange
        var rule = new AlertRule(
            Guid.NewGuid().ToString(),
            "Subscription Alert",
            AlertType.DeadLetterThreshold,
            AlertSeverity.Warning,
            5,
            true
        );
        _sut.AddRule(rule);

        var subscriptions = new[]
        {
            CreateSubscriptionInfo("test-topic", "test-sub", deadLetterCount: 10)
        };

        // Act
        var alerts = await _sut.EvaluateAlertsAsync(Enumerable.Empty<QueueInfo>(), subscriptions);

        // Assert
        alerts.Should().ContainSingle()
            .Which.EntityName.Should().Be("test-topic/test-sub");
    }

    [Fact]
    public async Task EvaluateAlertsAsync_WithEntityPattern_FiltersCorrectly()
    {
        // Arrange
        var rule = new AlertRule(
            Guid.NewGuid().ToString(),
            "Filtered Alert",
            AlertType.DeadLetterThreshold,
            AlertSeverity.Warning,
            5,
            true,
            EntityPattern: "prod-.*" // Only match queues starting with "prod-"
        );
        _sut.AddRule(rule);

        var queues = new[]
        {
            CreateQueueInfo("prod-queue1", deadLetterCount: 10),
            CreateQueueInfo("dev-queue1", deadLetterCount: 10)
        };

        // Act
        var alerts = await _sut.EvaluateAlertsAsync(queues, Enumerable.Empty<SubscriptionInfo>());

        // Assert
        alerts.Should().ContainSingle()
            .Which.EntityName.Should().Be("prod-queue1");
    }

    [Fact]
    public async Task EvaluateAlertsAsync_DoesNotTriggerDuplicateAlerts()
    {
        // Arrange
        var rule = CreateTestRule(threshold: 10);
        _sut.AddRule(rule);
        var queues = new[] { CreateQueueInfo("test-queue", deadLetterCount: 20) };

        // Act - Evaluate twice
        await _sut.EvaluateAlertsAsync(queues, Enumerable.Empty<SubscriptionInfo>());
        var secondAlerts = await _sut.EvaluateAlertsAsync(queues, Enumerable.Empty<SubscriptionInfo>());

        // Assert - Second evaluation should not produce new alerts
        secondAlerts.Should().BeEmpty();
        _sut.ActiveAlerts.Should().HaveCount(1);
    }

    [Fact]
    public async Task EvaluateAlertsAsync_RaisesAlertTriggeredEvent()
    {
        // Arrange
        AlertEvent? triggeredAlert = null;
        _sut.AlertTriggered += (_, alert) => triggeredAlert = alert;
        
        var rule = CreateTestRule(threshold: 10);
        _sut.AddRule(rule);
        var queues = new[] { CreateQueueInfo("test-queue", deadLetterCount: 20) };

        // Act
        await _sut.EvaluateAlertsAsync(queues, Enumerable.Empty<SubscriptionInfo>());

        // Assert
        triggeredAlert.Should().NotBeNull();
        triggeredAlert!.EntityName.Should().Be("test-queue");
    }

    #endregion

    #region Alert Acknowledgement Tests

    [Fact]
    public async Task AcknowledgeAlert_SetsIsAcknowledgedFlag()
    {
        // Arrange
        var rule = CreateTestRule(threshold: 10);
        _sut.AddRule(rule);
        var queues = new[] { CreateQueueInfo("test-queue", deadLetterCount: 20) };
        await _sut.EvaluateAlertsAsync(queues, Enumerable.Empty<SubscriptionInfo>());
        var alertId = _sut.ActiveAlerts.First().Id;

        // Act
        _sut.AcknowledgeAlert(alertId);

        // Assert
        _sut.ActiveAlerts.First(a => a.Id == alertId).IsAcknowledged.Should().BeTrue();
    }

    [Fact]
    public async Task AcknowledgeAlert_AllowsSameAlertToTriggerAgain()
    {
        // Arrange
        var rule = CreateTestRule(threshold: 10);
        _sut.AddRule(rule);
        var queues = new[] { CreateQueueInfo("test-queue", deadLetterCount: 20) };
        await _sut.EvaluateAlertsAsync(queues, Enumerable.Empty<SubscriptionInfo>());
        var alertId = _sut.ActiveAlerts.First().Id;

        // Act
        _sut.AcknowledgeAlert(alertId);
        var newAlerts = await _sut.EvaluateAlertsAsync(queues, Enumerable.Empty<SubscriptionInfo>());

        // Assert - Should trigger again after acknowledgement
        newAlerts.Should().ContainSingle();
    }

    [Fact]
    public async Task ClearAcknowledgedAlerts_RemovesOnlyAcknowledgedAlerts()
    {
        // Arrange
        var rule = CreateTestRule(threshold: 10);
        _sut.AddRule(rule);
        var queues = new[]
        {
            CreateQueueInfo("queue1", deadLetterCount: 20),
            CreateQueueInfo("queue2", deadLetterCount: 20)
        };
        await _sut.EvaluateAlertsAsync(queues, Enumerable.Empty<SubscriptionInfo>());
        
        // Acknowledge only the first alert
        var firstAlertId = _sut.ActiveAlerts.First().Id;
        _sut.AcknowledgeAlert(firstAlertId);

        // Act
        _sut.ClearAcknowledgedAlerts();

        // Assert
        _sut.ActiveAlerts.Should().ContainSingle()
            .Which.IsAcknowledged.Should().BeFalse();
    }

    #endregion

    #region Test Rule Tests

    [Fact]
    public void TestRule_CreatesTestAlert()
    {
        // Arrange
        var rule = CreateTestRule();

        // Act
        _sut.TestRule(rule);

        // Assert
        _sut.ActiveAlerts.Should().ContainSingle()
            .Which.Should().Match<AlertEvent>(a => 
                a.EntityType == "Test" && 
                a.EntityName == "[Test Entity]");
    }

    [Fact]
    public void TestRule_RaisesAlertTriggeredEvent()
    {
        // Arrange
        AlertEvent? triggeredAlert = null;
        _sut.AlertTriggered += (_, alert) => triggeredAlert = alert;
        var rule = CreateTestRule();

        // Act
        _sut.TestRule(rule);

        // Assert
        triggeredAlert.Should().NotBeNull();
    }

    #endregion

    #region Helper Methods

    private static AlertRule CreateTestRule(
        string name = "Test Rule",
        double threshold = 10,
        bool isEnabled = true,
        string? entityPattern = null)
    {
        return new AlertRule(
            Guid.NewGuid().ToString(),
            name,
            AlertType.DeadLetterThreshold,
            AlertSeverity.Warning,
            threshold,
            isEnabled,
            entityPattern
        );
    }

    private static QueueInfo CreateQueueInfo(
        string name,
        long activeMessageCount = 0,
        long deadLetterCount = 0,
        long sizeInBytes = 0)
    {
        return new QueueInfo(
            name,
            MessageCount: activeMessageCount + deadLetterCount,
            ActiveMessageCount: activeMessageCount,
            DeadLetterCount: deadLetterCount,
            ScheduledCount: 0,
            SizeInBytes: sizeInBytes,
            AccessedAt: DateTimeOffset.UtcNow,
            RequiresSession: false,
            DefaultMessageTtl: TimeSpan.FromDays(14),
            LockDuration: TimeSpan.FromSeconds(30)
        );
    }

    private static SubscriptionInfo CreateSubscriptionInfo(
        string topicName,
        string subscriptionName,
        long activeMessageCount = 0,
        long deadLetterCount = 0)
    {
        return new SubscriptionInfo(
            subscriptionName,
            topicName,
            MessageCount: activeMessageCount + deadLetterCount,
            ActiveMessageCount: activeMessageCount,
            DeadLetterCount: deadLetterCount,
            AccessedAt: DateTimeOffset.UtcNow,
            RequiresSession: false
        );
    }

    #endregion
}

