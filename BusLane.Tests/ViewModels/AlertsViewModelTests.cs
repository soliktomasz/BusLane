namespace BusLane.Tests.ViewModels;

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
}
