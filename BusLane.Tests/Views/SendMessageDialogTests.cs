namespace BusLane.Tests.Views;

using FluentAssertions;

public class SendMessageDialogTests
{
    [Fact]
    public void SendMessageDialog_UsesSharedDialogScaffold()
    {
        // Arrange
        var xaml = File.ReadAllText(GetDialogPath());

        // Assert
        xaml.Should().Contain("Classes=\"dialog-header\"");
        xaml.Should().Contain("Classes=\"dialog-body\"");
        xaml.Should().Contain("Classes=\"dialog-footer\"");
    }

    private static string GetDialogPath()
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
            "SendMessageDialog.axaml"));
    }
}
