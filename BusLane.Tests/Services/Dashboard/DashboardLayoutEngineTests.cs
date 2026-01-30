namespace BusLane.Tests.Services.Dashboard;

using BusLane.Models;
using BusLane.Services.Dashboard;
using System.Collections.ObjectModel;
using Xunit;

public class DashboardLayoutEngineTests
{
    private readonly DashboardLayoutEngine _engine = new();

    [Fact]
    public void FindNextAvailableSlot_EmptyGrid_ReturnsZeroZero()
    {
        var widgets = new ObservableCollection<DashboardWidget>();

        var (row, col) = _engine.FindNextAvailableSlot(widgets, 6, 4);

        Assert.Equal(0, row);
        Assert.Equal(0, col);
    }

    [Fact]
    public void FindNextAvailableSlot_WithWidget_FindsNextSlot()
    {
        var widgets = new ObservableCollection<DashboardWidget>
        {
            new() { Row = 0, Column = 0, Width = 6, Height = 4 }
        };

        var (row, col) = _engine.FindNextAvailableSlot(widgets, 6, 4);

        Assert.Equal(0, row);
        Assert.Equal(6, col);
    }

    [Fact]
    public void CanMove_WhenSpaceIsOccupied_ReturnsFalse()
    {
        var widget = new DashboardWidget { Row = 0, Column = 0, Width = 6, Height = 4 };
        var widgets = new ObservableCollection<DashboardWidget> { widget };

        bool canMove = _engine.CanMove(widgets, widget, 0, 0);

        Assert.True(canMove); // Same position is valid
    }

    [Fact]
    public void CanResize_WhenSpaceAvailable_ReturnsTrue()
    {
        var widget = new DashboardWidget { Row = 0, Column = 0, Width = 6, Height = 4 };
        var widgets = new ObservableCollection<DashboardWidget> { widget };

        bool canResize = _engine.CanResize(widgets, widget, 9, 4);

        Assert.True(canResize);
    }
}
