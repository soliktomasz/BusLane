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
        var sut = CreateSut(operations, navigation, confirmation);
        var queue = new QueueInfo("orders", 12, 10, 2, 0, 1024, null, false, TimeSpan.FromDays(14), TimeSpan.FromMinutes(1));

        // Act
        sut.OpenQueueDetailsCommand.Execute(queue);

        // Assert
        sut.ShowEntityDetailDialog.Should().BeTrue();
        sut.EntityDetailTitle.Should().Be("Queue Details");
        sut.EntityDetailRows.Should().Contain(row => row.Label == "Name" && row.Value == "orders");
    }

    [Fact]
    public void OpenTopicDetailsCommand_SetsDetailDialogState()
    {
        // Arrange
        var operations = Substitute.For<IServiceBusOperations>();
        var navigation = new NavigationState();
        var confirmation = new ConfirmationDialogViewModel();
        var sut = CreateSut(operations, navigation, confirmation);
        var topic = new TopicInfo("orders-topic", 1024, 2, null, TimeSpan.FromDays(14));

        // Act
        sut.OpenTopicDetailsCommand.Execute(topic);

        // Assert
        sut.ShowEntityDetailDialog.Should().BeTrue();
        sut.EntityDetailTitle.Should().Be("Topic Details");
        sut.EntityDetailRows.Should().Contain(row => row.Label == "Name" && row.Value == "orders-topic");
    }

    [Fact]
    public async Task SaveEntityEditCommand_WithInvalidQueueDuration_ShowsValidationAndDoesNotCallService()
    {
        // Arrange
        var operations = Substitute.For<IServiceBusOperations>();
        var navigation = new NavigationState();
        var confirmation = new ConfirmationDialogViewModel();
        var sut = CreateSut(operations, navigation, confirmation);
        var queue = new QueueInfo("orders", 0, 0, 0, 0, 1024, null, false, TimeSpan.FromDays(14), TimeSpan.FromMinutes(1));

        // Act
        sut.OpenQueueEditCommand.Execute(queue);
        sut.EditLockDuration = "not-a-duration";
        await sut.SaveEntityEditCommand.ExecuteAsync(null);

        // Assert
        sut.EntityEditValidationMessage.Should().Be("Lock duration must be a valid time span");
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
        var sut = CreateSut(operations, navigation, confirmation);
        var subscription = new SubscriptionInfo("processor", "orders-topic", 0, 0, 0, null, false);

        // Act
        sut.OpenSubscriptionEditCommand.Execute(subscription);
        sut.EditMaxDeliveryCount = "abc";
        await sut.SaveEntityEditCommand.ExecuteAsync(null);

        // Assert
        sut.EntityEditValidationMessage.Should().Be("Max delivery count must be a positive number");
        await operations.DidNotReceive().UpdateSubscriptionAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<SubscriptionUpdateOptions>(),
            Arg.Any<CancellationToken>());
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
        var sut = CreateSut(operations, navigation, confirmation);
        var subscription = new SubscriptionInfo("processor", "orders-topic", 0, 0, 0, null, false);

        // Act
        await sut.OpenSubscriptionRulesDialogCommand.ExecuteAsync(subscription);

        // Assert
        sut.ShowSubscriptionRulesDialog.Should().BeTrue();
        sut.RulesSubscription.Should().Be(subscription);
        sut.SubscriptionRules.Should().ContainSingle(rule => rule.Name == "important");
        await operations.Received(1).GetSubscriptionRulesAsync("orders-topic", "processor", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateSubscriptionRuleCommand_WithBlankRuleName_ShowsValidationAndDoesNotCallService()
    {
        // Arrange
        var operations = Substitute.For<IServiceBusOperations>();
        var navigation = new NavigationState();
        var confirmation = new ConfirmationDialogViewModel();
        var sut = CreateSut(operations, navigation, confirmation);
        await sut.OpenSubscriptionRulesDialogCommand.ExecuteAsync(new SubscriptionInfo("processor", "orders-topic", 0, 0, 0, null, false));
        operations.ClearReceivedCalls();

        // Act
        sut.NewRuleName = " ";
        sut.NewRuleSqlExpression = "1 = 1";
        await sut.CreateSubscriptionRuleCommand.ExecuteAsync(null);

        // Assert
        sut.SubscriptionRuleValidationMessage.Should().Be("Rule name is required");
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
        var sut = CreateSut(operations, navigation, confirmation);
        await sut.OpenSubscriptionRulesDialogCommand.ExecuteAsync(new SubscriptionInfo("processor", "orders-topic", 0, 0, 0, null, false));

        // Act
        sut.NewRuleName = "important";
        sut.NewRuleSqlExpression = "sys.Label = 'important'";
        await sut.CreateSubscriptionRuleCommand.ExecuteAsync(null);

        // Assert
        await operations.Received(1).CreateSubscriptionRuleAsync(
            "orders-topic",
            "processor",
            Arg.Is<SubscriptionRuleCreationOptions>(options =>
                options.Name == "important" &&
                options.FilterType == SubscriptionRuleFilterType.Sql &&
                options.SqlExpression == "sys.Label = 'important'"),
            Arg.Any<CancellationToken>());
        sut.SubscriptionRules.Should().ContainSingle(rule => rule.Name == "important");
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
        var sut = CreateSut(operations, navigation, confirmation);
        await sut.OpenSubscriptionRulesDialogCommand.ExecuteAsync(new SubscriptionInfo("processor", "orders-topic", 0, 0, 0, null, false));
        var rule = new SubscriptionRuleInfo("important", SubscriptionRuleFilterType.Sql, "1 = 1", "1 = 1");

        // Act
        sut.DeleteSubscriptionRuleRequestCommand.Execute(rule);

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
        var sut = CreateSut(operations, navigation, confirmation);
        var queue = new QueueInfo("orders", 0, 0, 0, 0, 1024, null, false, TimeSpan.FromDays(14), TimeSpan.FromMinutes(1));

        // Act
        sut.DeleteQueueRequestCommand.Execute(queue);

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
        var sut = CreateSut(operations, navigation, confirmation);
        var topic = new TopicInfo("orders-topic", 1024, 0, null, TimeSpan.FromDays(14));

        // Act
        sut.DeleteTopicRequestCommand.Execute(topic);

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
        var sut = CreateSut(operations, navigation, confirmation);
        var subscription = new SubscriptionInfo("processor", "orders-topic", 0, 0, 0, null, false);

        // Act
        sut.DeleteSubscriptionRequestCommand.Execute(subscription);

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
        ConfirmationDialogViewModel confirmation)
    {
        return new EntityOperationsViewModel(
            () => operations,
            () => navigation,
            _ => { },
            confirmation);
    }
}
