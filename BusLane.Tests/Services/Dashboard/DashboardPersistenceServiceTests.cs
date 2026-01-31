namespace BusLane.Tests.Services.Dashboard;

using BusLane.Models;
using BusLane.Services.Dashboard;
using System.IO;
using Xunit;

public class DashboardPersistenceServiceTests
{
    private readonly string _testPath = Path.Combine(Path.GetTempPath(), $"test_dashboard_{Guid.NewGuid()}.json");

    [Fact]
    public void SaveAndLoad_Roundtrip_PreservesConfiguration()
    {
        var service = new DashboardPersistenceService(_testPath);
        var config = new DashboardConfiguration
        {
            Widgets =
            [
                new DashboardWidget { Type = WidgetType.LineChart, Row = 0, Column = 0, Width = 6, Height = 4 }
            ]
        };

        service.Save(config);
        var loaded = service.Load();

        Assert.Single(loaded.Widgets);
        Assert.Equal(WidgetType.LineChart, loaded.Widgets[0].Type);
    }

    [Fact]
    public void Load_WhenFileDoesNotExist_ReturnsDefaultConfiguration()
    {
        var nonExistentPath = Path.Combine(Path.GetTempPath(), $"nonexistent_{Guid.NewGuid()}.json");
        var service = new DashboardPersistenceService(nonExistentPath);

        var loaded = service.Load();

        Assert.Equal(4, loaded.Widgets.Count);
    }
}
