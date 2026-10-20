namespace BusLane.Tests.ViewModels;

using BusLane.Models;
using BusLane.Services.Monitoring;
using BusLane.ViewModels;
using FluentAssertions;
using NSubstitute;

public class AlertsViewModelTests
{
    [Fact]
    public void Dispose_UnsubscribesFromAlertChanges()
    {
        // Arrange - the alert service is a singleton, so a closed panel must unsubscribe
        var alertService = Substitute.For<IAlertService>();
        alertService.Rules.Returns([]);
        alertService.ActiveAlerts.Returns([]);
        alertService.History.Returns([]);
        var sut = new AlertsViewModel(alertService, Substitute.For<INotificationService>(), () => { });

        // Act
        sut.Dispose();

        // Assert
        alertService.Received(1).AlertsChanged -= Arg.Any<EventHandler>();
    }

    [Fact]
    public void ShowEditRule_WithRule_LoadsTypeSeverityAndThreshold()
    {
        // Arrange
        var alertService = Substitute.For<IAlertService>();
        alertService.Rules.Returns([]);
        alertService.ActiveAlerts.Returns([]);
        alertService.History.Returns([]);
        var sut = new AlertsViewModel(alertService, Substitute.For<INotificationService>(), () => { });
        var rule = new AlertRule("r1", "Backlog", AlertType.QueueSizeThreshold, AlertSeverity.Critical, 5000);

        // Act
        sut.ShowEditRuleCommand.Execute(rule);
        sut.SaveRuleCommand.Execute(null);

        // Assert - saving an unchanged edit must keep the rule's condition
        alertService.Received(1).UpdateRule(rule);
    }
}
