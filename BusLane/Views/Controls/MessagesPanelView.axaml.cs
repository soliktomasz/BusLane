namespace BusLane.Views.Controls;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

public partial class MessagesPanelView : UserControl
{
    private (bool Stacked, bool Visible)? _inspectorLayout;

    public MessagesPanelView()
    {
        InitializeComponent();
        SizeChanged += (_, _) =>
        {
            // Narrow workspaces keep the entity title readable by dropping secondary action labels.
            Classes.Set("narrow-header", Bounds.Width < 760);
            UpdateInspectorLayout();
        };
        InspectorHost.PropertyChanged += (_, e) =>
        {
            if (e.Property == IsVisibleProperty)
                UpdateInspectorLayout();
        };
        AddHandler(SelectingItemsControl.SelectionChangedEvent, (_, e) =>
        {
            if (e.Source is ListBox { Classes: var classes } && classes.Contains("message-table") && e.AddedItems.Count > 0)
                InspectorPanel.PlaySelectionMotion();
        });
    }

    // Choosing a menu action closes the More menu, as a native menu would.
    private void OnMoreMenuItemClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        MoreActionsButton.Flyout?.Hide();

    private void UpdateInspectorLayout()
    {
        var stacked = Bounds.Width < 660;
        // Small windows keep the table usable; every row can still open full details.
        var visible = InspectorHost.IsVisible && (!stacked || Bounds.Height >= 640);
        var layout = (Stacked: stacked, Visible: visible);
        if (_inspectorLayout == layout)
            return;

        _inspectorLayout = layout;
        stacked = layout.Stacked && layout.Visible;
        InspectorPanel.IsVisible = layout.Visible;
        InspectorSplitter.IsVisible = layout.Visible;
        InspectorWorkspaceGrid.ColumnDefinitions = new ColumnDefinitions(
            !layout.Visible ? "*,0,0" : stacked ? "*" : "*,5,360");
        InspectorWorkspaceGrid.RowDefinitions = new RowDefinitions(stacked ? "*,5,*" : "*");
        Grid.SetColumn(InspectorSplitter, stacked ? 0 : 1);
        Grid.SetRow(InspectorSplitter, stacked ? 1 : 0);
        Grid.SetColumn(InspectorHost, stacked ? 0 : 2);
        Grid.SetRow(InspectorHost, stacked ? 2 : 0);
        InspectorSplitter.ResizeDirection = stacked ? GridResizeDirection.Rows : GridResizeDirection.Columns;
        if (layout.Visible && !stacked)
        {
            InspectorWorkspaceGrid.ColumnDefinitions[0].MinWidth = 300;
            InspectorWorkspaceGrid.ColumnDefinitions[2].MinWidth = 300;
        }
    }

}
