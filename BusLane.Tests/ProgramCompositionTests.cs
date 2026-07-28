namespace BusLane.Tests;

using System.Reflection;
using BusLane.Services.ServiceBus;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

public class ProgramCompositionTests
{
    [Fact]
    public void ConfigureServices_RegistersCorrelationInvestigationServices()
    {
        // Arrange
        var services = new ServiceCollection();
        var programType = typeof(BusLane.ViewModels.MainWindowViewModel).Assembly
            .GetType("BusLane.Program", throwOnError: true)!;
        var configureServices = programType.GetMethod(
            "ConfigureServices",
            BindingFlags.NonPublic | BindingFlags.Static)!;

        // Act
        configureServices.Invoke(null, [services]);
        using var provider = services.BuildServiceProvider();

        // Assert
        provider.GetRequiredService<ICorrelationMessageCatalog>()
            .Should().BeOfType<CorrelationMessageCatalog>();
        provider.GetRequiredService<ICorrelationRefreshDelay>()
            .Should().BeOfType<CorrelationRefreshDelay>();
        provider.GetRequiredService<ICorrelationMessageComparisonService>()
            .Should().BeOfType<CorrelationMessageComparisonService>();
    }
}
