using BusLane.Models.Dashboard;
using BusLane.Services.Dashboard;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace BusLane.Tests.Services.Dashboard;

public class DashboardRefreshServiceTests
{
    private readonly DashboardRefreshService _sut;

    public DashboardRefreshServiceTests()
    {
        _sut = new DashboardRefreshService();
    }

    [Fact]
    public void Constructor_InitializesProperties()
    {
        // Assert
        _sut.LastRefreshTime.Should().BeNull();
        _sut.IsRefreshing.Should().BeFalse();
    }
}
