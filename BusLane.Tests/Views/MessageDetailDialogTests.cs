namespace BusLane.Tests.Views;

using FluentAssertions;

public class MessageDetailDialogTests
{
    [Fact]
    public void MessageDetailDialog_BindsMessageFieldsFromSelectedMessageDataContext()
    {
        // Arrange
        var xaml = File.ReadAllText(GetMessageDetailDialogPath());

        // Assert
        xaml.Should().Contain("DataContext=\"{Binding $parent[UserControl].DataContext.CurrentMessageOps.SelectedMessage}\"");
        xaml.Should().NotContain("CurrentMessageOps.SelectedMessage.");
    }

    private static string GetMessageDetailDialogPath()
    {
        return Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "BusLane",
            "Views",
            "Dialogs",
            "MessageDetailDialog.axaml"));
    }
}
