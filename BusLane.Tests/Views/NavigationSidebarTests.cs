namespace BusLane.Tests.Views;

using FluentAssertions;

public class NavigationSidebarTests
{
    [Fact]
    public void NavigationSidebar_HasSingleAzureLoginButton()
    {
        // Arrange
        var xaml = File.ReadAllText(GetSidebarPath());

        // Act
        var loginButtonCount = CountOccurrences(xaml, "Command=\"{Binding LoginCommand}\"");

        // Assert
        loginButtonCount.Should().Be(1);
    }

    [Fact]
    public void NavigationSidebar_DoesNotRenderContextualActionsSection()
    {
        // Arrange
        var xaml = File.ReadAllText(GetSidebarPath());

        // Act
        var hasContextualActionsSection = xaml.Contains("Text=\"Contextual actions\"", StringComparison.Ordinal);

        // Assert
        hasContextualActionsSection.Should().BeFalse();
    }

    [Fact]
    public void NavigationSidebar_HasSingleMyConnectionsButton()
    {
        // Arrange
        var xaml = File.ReadAllText(GetSidebarPath());

        // Act
        var myConnectionsButtonCount = CountOccurrences(xaml, "Command=\"{Binding OpenConnectionLibraryCommand}\"");

        // Assert
        myConnectionsButtonCount.Should().Be(1);
    }

    [Fact]
    public void NavigationSidebar_UsesCompactWorkspaceSummary()
    {
        // Arrange
        var xaml = File.ReadAllText(GetSidebarPath());

        // Assert
        xaml.Should().Contain("Classes=\"sidebar-workspace-summary\"");
        xaml.Should().NotContain("Classes=\"sidebar-workspace-card\"");
    }

    [Fact]
    public void NavigationSidebar_DoesNotRenderDisconnectAsStandalonePrimaryBlock()
    {
        // Arrange
        var xaml = File.ReadAllText(GetSidebarPath());

        // Assert
        xaml.Should().NotContain("Classes=\"danger-outline\"");
        xaml.Should().Contain("Command=\"{Binding DisconnectConnectionCommand}\"");
    }

    [Fact]
    public void NavigationSidebar_DoesNotRenderSelectedEntityStrip()
    {
        // Arrange
        var xaml = File.ReadAllText(GetSidebarPath());

        // Act
        var hasSelectedEntityBinding = xaml.Contains("CurrentNavigation.CurrentEntityName", StringComparison.Ordinal);

        // Assert
        hasSelectedEntityBinding.Should().BeFalse();
    }

    [Fact]
    public void NavigationSidebar_DoesNotRenderConnectionTypeBadge()
    {
        // Arrange
        var xaml = File.ReadAllText(GetSidebarPath());

        // Act
        var hasConnectionTypeBadge = xaml.Contains("ActiveTab.SavedConnection.TypeDisplayName", StringComparison.Ordinal);

        // Assert
        hasConnectionTypeBadge.Should().BeFalse();
    }

    [Fact]
    public void NavigationSidebar_UsesFullWidthWorkspaceActions()
    {
        // Arrange
        var xaml = File.ReadAllText(GetSidebarPath());

        // Assert
        xaml.Should().Contain("<StackPanel Classes=\"sidebar-workspace-actions\"");
        xaml.Should().NotContain("<WrapPanel Classes=\"sidebar-workspace-actions\"");
        xaml.Should().NotContain("ItemWidth=\"120\"");
    }

    [Fact]
    public void NavigationSidebar_DoesNotRenderRefreshWorkspaceAction()
    {
        // Arrange
        var xaml = File.ReadAllText(GetSidebarPath());

        // Assert
        xaml.Should().NotContain("Command=\"{Binding RefreshConnectionCommand}\"");
        xaml.Should().NotContain("Text=\"Refresh\"");
    }

    private static string GetSidebarPath()
    {
        return Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "BusLane",
            "Views",
            "Controls",
            "NavigationSidebar.axaml"));
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var index = 0;

        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }
}
