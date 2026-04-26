namespace BusLane.Tests.Views;

using FluentAssertions;

public class TabBarViewTests
{
    [Fact]
    public void TabBarView_DoesNotUsePointerPressedBorderTabs()
    {
        // Arrange
        var xaml = File.ReadAllText(GetTabBarPath());

        // Assert
        xaml.Should().NotContain("PointerPressed=\"TabBorder_PointerPressed\"");
        xaml.Should().NotContain("Classes=\"tab-border\"");
    }

    [Fact]
    public void TabBarView_UsesAccessibleTabButtons()
    {
        // Arrange
        var xaml = File.ReadAllText(GetTabBarPath());

        // Assert
        xaml.Should().Contain("<RadioButton");
        xaml.Should().Contain("GroupName=\"ConnectionTabs\"");
        xaml.Should().Contain("IsCheckedChanged=\"TabRadioButton_Checked\"");
        xaml.Should().Contain("AutomationProperties.Name=\"Close tab\"");
    }

    private static string GetTabBarPath()
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
            "TabBarView.axaml"));
    }
}
