namespace BusLane.Tests;

using Avalonia.Controls;
using FluentAssertions;

public class AppMenuTests
{
    [Fact]
    public void AppXaml_DefinesMacApplicationAboutMenu()
    {
        // Arrange
        var xaml = File.ReadAllText(GetAppPath());

        // Assert
        xaml.Should().Contain("<NativeMenu.Menu>");
        xaml.Should().Contain("<NativeMenuItem Header=\"About BusLane\" Click=\"AboutMenuItem_OnClick\"/>");
        xaml.Should().Contain("<NativeMenuItem Header=\"Check for Updates...\" Click=\"CheckForUpdatesMenuItem_OnClick\"/>");
    }

    [Fact]
    public void CreateMacMenu_Always_CreatesViewAndHelpMenusWithExpectedItems()
    {
        // Arrange
        EventHandler onToggleFullscreenClick = (_, _) => { };
        EventHandler onShowLogViewerClick = (_, _) => { };
        EventHandler onShowTerminalClick = (_, _) => { };
        EventHandler onCheckForUpdatesClick = (_, _) => { };
        EventHandler onAboutClick = (_, _) => { };

        // Act
        var menu = App.CreateMacMenu(onToggleFullscreenClick, onShowLogViewerClick, onShowTerminalClick, onCheckForUpdatesClick, onAboutClick);

        // Assert
        menu.Items.Should().HaveCount(2);

        var viewItem = menu.Items[0].Should().BeOfType<NativeMenuItem>().Subject;
        viewItem.Header.Should().Be("View");
        viewItem.Menu.Should().NotBeNull();
        viewItem.Menu!.Items.Should().HaveCount(3);

        var toggleFullscreenItem = viewItem.Menu.Items[0].Should().BeOfType<NativeMenuItem>().Subject;
        toggleFullscreenItem.Header.Should().Be("Toggle Fullscreen");
        toggleFullscreenItem.HasClickHandlers.Should().BeTrue();

        var showLogViewerItem = viewItem.Menu.Items[1].Should().BeOfType<NativeMenuItem>().Subject;
        showLogViewerItem.Header.Should().Be("Show Log Viewer");
        showLogViewerItem.HasClickHandlers.Should().BeTrue();

        var showTerminalItem = viewItem.Menu.Items[2].Should().BeOfType<NativeMenuItem>().Subject;
        showTerminalItem.Header.Should().Be("Show Terminal");
        showTerminalItem.HasClickHandlers.Should().BeTrue();

        var helpItem = menu.Items[1].Should().BeOfType<NativeMenuItem>().Subject;
        helpItem.Header.Should().Be("Help");
        helpItem.Menu.Should().NotBeNull();
        helpItem.Menu!.Items.Should().HaveCount(2);

        var aboutItem = helpItem.Menu.Items[0].Should().BeOfType<NativeMenuItem>().Subject;
        aboutItem.Header.Should().Be("About BusLane");
        aboutItem.HasClickHandlers.Should().BeTrue();

        var checkForUpdatesItem = helpItem.Menu.Items[1].Should().BeOfType<NativeMenuItem>().Subject;
        checkForUpdatesItem.Header.Should().Be("Check for Updates...");
        checkForUpdatesItem.HasClickHandlers.Should().BeTrue();
    }

    [Fact]
    public async Task RunCheckForUpdatesMenuActionAsync_WhenCheckCompletes_ShowsResultMessage()
    {
        // Arrange
        var item = new NativeMenuItem("Check for Updates...");
        var sawCheckingState = false;

        // Act
        await App.RunCheckForUpdatesMenuActionAsync(item, () =>
        {
            sawCheckingState = item.Header?.ToString() == "Checking for Updates..." && !item.IsEnabled;
            return Task.FromResult("BusLane is up to date.");
        });

        // Assert
        sawCheckingState.Should().BeTrue();
        item.Header.Should().Be("BusLane is up to date.");
        item.IsEnabled.Should().BeTrue();
    }

    private static string GetAppPath()
    {
        return Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "BusLane",
            "App.axaml"));
    }
}
