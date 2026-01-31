namespace BusLane.Services.Dashboard;

using BusLane.Models;
using System.Collections.ObjectModel;

/// <summary>
/// Manages widget placement in a 12-column grid, handling slot finding and collision detection.
/// </summary>
public class DashboardLayoutEngine
{
    private const int GridColumns = 12;

    /// <summary>Finds the next available grid slot that can fit a widget of the given dimensions.</summary>
    public (int row, int col) FindNextAvailableSlot(ObservableCollection<DashboardWidget> widgets, int width, int height)
    {
        if (widgets.Count == 0)
            return (0, 0);

        int maxRow = widgets.Any() ? widgets.Max(w => w.Row + w.Height) : 0;

        for (int row = 0; row <= maxRow + height; row++)
        {
            for (int col = 0; col <= GridColumns - width; col++)
            {
                if (IsSpaceAvailable(widgets, null, row, col, width, height))
                    return (row, col);
            }
        }

        return (maxRow, 0);
    }

    /// <summary>Returns whether a widget can be moved to the given position without overlapping others.</summary>
    public bool CanMove(ObservableCollection<DashboardWidget> widgets, DashboardWidget widget, int newRow, int newCol)
    {
        return IsSpaceAvailable(widgets, widget, newRow, newCol, widget.Width, widget.Height);
    }

    /// <summary>Returns whether a widget can be resized to the given dimensions without overlapping others or exceeding grid bounds.</summary>
    public bool CanResize(ObservableCollection<DashboardWidget> widgets, DashboardWidget widget, int newWidth, int newHeight)
    {
        if (newWidth < 3 || newHeight < 2 || widget.Column + newWidth > GridColumns)
            return false;

        return IsSpaceAvailable(widgets, widget, widget.Row, widget.Column, newWidth, newHeight);
    }

    private bool IsSpaceAvailable(ObservableCollection<DashboardWidget> widgets, DashboardWidget? excludeWidget, int row, int col, int width, int height)
    {
        if (col + width > GridColumns || col < 0 || row < 0)
            return false;

        foreach (var w in widgets)
        {
            if (w == excludeWidget)
                continue;

            bool overlap = !(w.Row + w.Height <= row || w.Row >= row + height ||
                           w.Column + w.Width <= col || w.Column >= col + width);

            if (overlap)
                return false;
        }

        return true;
    }
}
