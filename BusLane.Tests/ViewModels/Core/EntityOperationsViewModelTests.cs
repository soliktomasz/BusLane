namespace BusLane.Tests.ViewModels.Core;

using BusLane.Models;
using BusLane.Services.ServiceBus;
using BusLane.ViewModels.Core;
using FluentAssertions;
using NSubstitute;

public class EntityOperationsViewModelTests
{
    [Fact]
    public void OpenQueueDetailsCommand_SetsDetailDialogState()
    {
        // Arrange
        var operations = Substitute.For<IServiceBusOperations>();
        var navigation = new NavigationState();
        var confirmation = new ConfirmationDialogViewModel();
        var _sut = CreateSut(operations, navigation, confirmation);
        var queue = new QueueInfo("orders", 12, 10, 2, 0, 1024, null, false, TimeSpan.FromDays(14), TimeSpan.FromMinutes(1));

        // Act
        _sut.OpenQueueDetailsCommand.Execute(queue);

        // Assert
        _sut.ShowEntityDetailDialog.Should().BeTrue();
        _sut.EntityDetailTitle.Should().Be("Queue Details");
        _sut.EntityDetailRows.Should().Contain(row => row.Label == "Name" && row.Value == "orders");
    }

    [Fact]
    public void OpenTopicDetailsCommand_SetsDetailDialogState()
    {
        // Arrange
        var operations = Substitute.For<IServiceBusOperations>();
        var navigation = new NavigationState();
        var confirmation = new ConfirmationDialogViewModel();
        var _sut = CreateSut(operations, navigation, confirmation);
        var topic = new TopicInfo("orders-topic", 1024, 2, null, TimeSpan.FromDays(14));

        // Act
        _sut.OpenTopicDetailsCommand.Execute(topic);

        // Assert
        _sut.ShowEntityDetailDialog.Should().BeTrue();
        _sut.EntityDetailTitle.Should().Be("Topic Details");
        _sut.EntityDetailRows.Should().Contain(row => row.Label == "Name" && row.Value == "orders-topic");
    }

    [Fact]
    public async Task SaveEntityEditCommand_WithInvalidQueueDuration_ShowsValidationAndDoesNotCallService()
    {
        // Arrange
        var operations = Substitute.For<IServiceBusOperations>();
        var navigation = new NavigationState();
        var confirmation = new ConfirmationDialogViewModel();
        var _sut = CreateSut(operations, navigation, confirmation);
        var queue = new QueueInfo("orders", 0, 0, 0, 0, 1024, null, false, TimeSpan.FromDays(14), TimeSpan.FromMinutes(1));

        // Act
        _sut.OpenQueueEditCommand.Execute(queue);
        _sut.EditLockDuration = "not-a-duration";
        await _sut.SaveEntityEditCommand.ExecuteAsync(null);

        // Assert
        _sut.EntityEditValidationMessage.Should().Be("Lock duration must be a valid time span");
        await operations.DidNotReceive().UpdateQueueAsync(
            Arg.Any<string>(),
            Arg.Any<QueueUpdateOptions>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveEntityEditCommand_WithInvalidSubscriptionMaxDeliveryCount_ShowsValidationAndDoesNotCallService()
    {
        // Arrange
        var operations = Substitute.For<IServiceBusOperations>();
        var navigation = new NavigationState();
        var confirmation = new ConfirmationDialogViewModel();
        var _sut = CreateSut(operations, navigation, confirmation);
        var subscription = new SubscriptionInfo("processor", "orders-topic", 0, 0, 0, null, false);

        // Act
        _sut.OpenSubscriptionEditCommand.Execute(subscription);
        _sut.EditMaxDeliveryCount = "abc";
        await _sut.SaveEntityEditCommand.ExecuteAsync(null);

        // Assert
        _sut.EntityEditValidationMessage.Should().Be("Max delivery count must be a positive number");
        await operations.DidNotReceive().UpdateSubscriptionAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<SubscriptionUpdateOptions>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveEntityEditCommand_WhenQueueUpdateFails_ShowsStatusAndResetsUpdatingState()
    {
        // Arrange
        var operations = Substitute.For<IServiceBusOperations>();
        operations.UpdateQueueAsync("orders", Arg.Any<QueueUpdateOptions>(), Arg.Any<CancellationToken>())
            .Returns(_ => throw new InvalidOperationException("network down"));
        var navigation = new NavigationState();
        var confirmation = new ConfirmationDialogViewModel();
        var statusMessage = string.Empty;
        var _sut = CreateSut(operations, navigation, confirmation, message => statusMessage = message);
        var queue = new QueueInfo("orders", 0, 0, 0, 0, 1024, null, false, TimeSpan.FromDays(14), TimeSpan.FromMinutes(1));

        // Act
        _sut.OpenQueueEditCommand.Execute(queue);
        _sut.EditLockDuration = "00:02:00";
        await _sut.SaveEntityEditCommand.ExecuteAsync(null);

        // Assert
        statusMessage.Should().Be("Unable to update queue: network down");
        _sut.IsUpdatingEntity.Should().BeFalse();
        _sut.ShowEntityEditDialog.Should().BeTrue();
    }

    [Fact]
    public async Task SaveEntityEditCommand_WhenTopicUpdateSucceeds_ReplacesStaleTopicInfo()
    {
        // Arrange
        var operations = Substitute.For<IServiceBusOperations>();
        operations.UpdateTopicAsync("orders-topic", Arg.Any<TopicUpdateOptions>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var refreshed = new TopicInfo("orders-topic", 2048, 1, null, TimeSpan.FromHours(2));
        operations.GetTopicInfoAsync("orders-topic", Arg.Any<CancellationToken>())
            .Returns(refreshed);
        var navigation = new NavigationState();
        var topic = new TopicInfo("orders-topic", 1024, 1, null, TimeSpan.FromDays(14))
        {
            SubscriptionsLoaded = true,
            IsExpanded = true
        };
        var subscription = new SubscriptionInfo("processor", "orders-topic", 0, 0, 0, null, false);
        topic.Subscriptions.Add(subscription);
        navigation.Topics.Add(topic);
        navigation.SelectedTopic = topic;
        navigation.SelectedEntity = topic;
        var confirmation = new ConfirmationDialogViewModel();
        var _sut = CreateSut(operations, navigation, confirmation);

        // Act
        _sut.OpenTopicEditCommand.Execute(topic);
        _sut.EditDefaultMessageTimeToLive = "02:00:00";
        await _sut.SaveEntityEditCommand.ExecuteAsync(null);

        // Assert
        navigation.Topics.Should().ContainSingle();
        navigation.Topics[0].DefaultMessageTtl.Should().Be(TimeSpan.FromHours(2));
        navigation.Topics[0].Subscriptions.Should().ContainSingle().Which.Should().Be(subscription);
        navigation.Topics[0].SubscriptionsLoaded.Should().BeTrue();
        navigation.Topics[0].IsExpanded.Should().BeTrue();
        navigation.SelectedTopic.Should().Be(navigation.Topics[0]);
        navigation.SelectedEntity.Should().Be(navigation.Topics[0]);
    }

    [Fact]
    public async Task OpenSubscriptionRulesDialogCommand_LoadsRulesForSubscription()
    {
        // Arrange
        var operations = Substitute.For<IServiceBusOperations>();
        var rules = new[]
        {
            new SubscriptionRuleInfo("important", SubscriptionRuleFilterType.Sql, "sys.Label = 'important'", "sys.Label = 'important'")
        };
        operations.GetSubscriptionRulesAsync("orders-topic", "processor", Arg.Any<CancellationToken>())
            .Returns(rules);
        var navigation = new NavigationState();
        var confirmation = new ConfirmationDialogViewModel();
        var _sut = CreateSut(operations, navigation, confirmation);
        var subscription = new SubscriptionInfo("processor", "orders-topic", 0, 0, 0, null, false);

        // Act
        await _sut.OpenSubscriptionRulesDialogCommand.ExecuteAsync(subscription);

        // Assert
        _sut.ShowSubscriptionRulesDialog.Should().BeTrue();
        _sut.RulesSubscription.Should().Be(subscription);
        _sut.SubscriptionRules.Should().ContainSingle(rule => rule.Name == "important");
        await operations.Received(1).GetSubscriptionRulesAsync("orders-topic", "processor", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateSubscriptionRuleCommand_WithBlankRuleName_ShowsValidationAndDoesNotCallService()
    {
        // Arrange
        var operations = Substitute.For<IServiceBusOperations>();
        var navigation = new NavigationState();
        var confirmation = new ConfirmationDialogViewModel();
        var _sut = CreateSut(operations, navigation, confirmation);
        await _sut.OpenSubscriptionRulesDialogCommand.ExecuteAsync(new SubscriptionInfo("processor", "orders-topic", 0, 0, 0, null, false));
        operations.ClearReceivedCalls();

        // Act
        _sut.NewRuleName = " ";
        _sut.NewRuleSqlExpression = "1 = 1";
        await _sut.CreateSubscriptionRuleCommand.ExecuteAsync(null);

        // Assert
        _sut.SubscriptionRuleValidationMessage.Should().Be("Rule name is required");
        await operations.DidNotReceive().CreateSubscriptionRuleAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<SubscriptionRuleCreationOptions>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateSubscriptionRuleCommand_WithSqlRule_CreatesRuleAndReloads()
    {
        // Arrange
        var operations = Substitute.For<IServiceBusOperations>();
        operations.GetSubscriptionRulesAsync("orders-topic", "processor", Arg.Any<CancellationToken>())
            .Returns(
                Task.FromResult<IReadOnlyList<SubscriptionRuleInfo>>([]),
                Task.FromResult<IReadOnlyList<SubscriptionRuleInfo>>(
                [
                    new SubscriptionRuleInfo("important", SubscriptionRuleFilterType.Sql, "sys.Label = 'important'", "sys.Label = 'important'")
                ]));
        var navigation = new NavigationState();
        var confirmation = new ConfirmationDialogViewModel();
        var _sut = CreateSut(operations, navigation, confirmation);
        await _sut.OpenSubscriptionRulesDialogCommand.ExecuteAsync(new SubscriptionInfo("processor", "orders-topic", 0, 0, 0, null, false));

        // Act
        _sut.NewRuleName = "important";
        _sut.NewRuleSqlExpression = "sys.Label = 'important'";
        await _sut.CreateSubscriptionRuleCommand.ExecuteAsync(null);

        // Assert
        await operations.Received(1).CreateSubscriptionRuleAsync(
            "orders-topic",
            "processor",
            Arg.Is<SubscriptionRuleCreationOptions>(options =>
                options.Name == "important" &&
                options.FilterType == SubscriptionRuleFilterType.Sql &&
                options.SqlExpression == "sys.Label = 'important'"),
            Arg.Any<CancellationToken>());
        _sut.SubscriptionRules.Should().ContainSingle(rule => rule.Name == "important");
    }

    [Fact]
    public async Task DeleteSubscriptionRuleRequestCommand_RequestsConfirmationBeforeDeleting()
    {
        // Arrange
        var operations = Substitute.For<IServiceBusOperations>();
        operations.GetSubscriptionRulesAsync("orders-topic", "processor", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<SubscriptionRuleInfo>>([]));
        var navigation = new NavigationState();
        var confirmation = new ConfirmationDialogViewModel();
        var _sut = CreateSut(operations, navigation, confirmation);
        await _sut.OpenSubscriptionRulesDialogCommand.ExecuteAsync(new SubscriptionInfo("processor", "orders-topic", 0, 0, 0, null, false));
        var rule = new SubscriptionRuleInfo("important", SubscriptionRuleFilterType.Sql, "1 = 1", "1 = 1");

        // Act
        _sut.DeleteSubscriptionRuleRequestCommand.Execute(rule);

        // Assert
        confirmation.ShowConfirmDialog.Should().BeTrue();
        await operations.DidNotReceive().DeleteSubscriptionRuleAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());

        // Act
        await confirmation.ExecuteConfirmDialogAsync();

        // Assert
        await operations.Received(1).DeleteSubscriptionRuleAsync("orders-topic", "processor", "important", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteQueueRequestCommand_RequestsConfirmationBeforeDeleting()
    {
        // Arrange
        var operations = Substitute.For<IServiceBusOperations>();
        var navigation = new NavigationState();
        var confirmation = new ConfirmationDialogViewModel();
        var _sut = CreateSut(operations, navigation, confirmation);
        var queue = new QueueInfo("orders", 0, 0, 0, 0, 1024, null, false, TimeSpan.FromDays(14), TimeSpan.FromMinutes(1));

        // Act
        _sut.DeleteQueueRequestCommand.Execute(queue);

        // Assert
        confirmation.ShowConfirmDialog.Should().BeTrue();
        await operations.DidNotReceive().DeleteQueueAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());

        // Act
        await confirmation.ExecuteConfirmDialogAsync();

        // Assert
        await operations.Received(1).DeleteQueueAsync("orders", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteTopicRequestCommand_RequestsConfirmationBeforeDeleting()
    {
        // Arrange
        var operations = Substitute.For<IServiceBusOperations>();
        var navigation = new NavigationState();
        var confirmation = new ConfirmationDialogViewModel();
        var _sut = CreateSut(operations, navigation, confirmation);
        var topic = new TopicInfo("orders-topic", 1024, 0, null, TimeSpan.FromDays(14));

        // Act
        _sut.DeleteTopicRequestCommand.Execute(topic);

        // Assert
        confirmation.ShowConfirmDialog.Should().BeTrue();
        await operations.DidNotReceive().DeleteTopicAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());

        // Act
        await confirmation.ExecuteConfirmDialogAsync();

        // Assert
        await operations.Received(1).DeleteTopicAsync("orders-topic", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteSubscriptionRequestCommand_RequestsConfirmationBeforeDeleting()
    {
        // Arrange
        var operations = Substitute.For<IServiceBusOperations>();
        operations.GetSubscriptionsAsync("orders-topic", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IEnumerable<SubscriptionInfo>>([]));
        var navigation = new NavigationState();
        navigation.Topics.Add(new TopicInfo("orders-topic", 1024, 1, null, TimeSpan.FromDays(14)));
        var confirmation = new ConfirmationDialogViewModel();
        var _sut = CreateSut(operations, navigation, confirmation);
        var subscription = new SubscriptionInfo("processor", "orders-topic", 0, 0, 0, null, false);

        // Act
        _sut.DeleteSubscriptionRequestCommand.Execute(subscription);

        // Assert
        confirmation.ShowConfirmDialog.Should().BeTrue();
        await operations.DidNotReceive().DeleteSubscriptionAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());

        // Act
        await confirmation.ExecuteConfirmDialogAsync();

        // Assert
        await operations.Received(1).DeleteSubscriptionAsync("orders-topic", "processor", Arg.Any<CancellationToken>());
    }

    private static EntityOperationsViewModel CreateSut(
        IServiceBusOperations operations,
        NavigationState navigation,
        ConfirmationDialogViewModel confirmation,
        Action<string>? setStatusMessage = null)
    {
        return new EntityOperationsViewModel(
            () => operations,
            () => navigation,
            setStatusMessage ?? (_ => { }),
            confirmation);
    }
}
