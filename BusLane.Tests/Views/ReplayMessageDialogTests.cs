namespace BusLane.Tests.Views;

using FluentAssertions;

public class ReplayMessageDialogTests
{
    [Fact]
    public void ReplayDialog_UsesSharedDialogScaffoldAndSafetyControls()
    {
        // Arrange
        var xaml = File.ReadAllText(GetDialogPath());

        // Assert
        xaml.Should().Contain("Classes=\"dialog-header\"");
        xaml.Should().Contain("Classes=\"dialog-body\"");
        xaml.Should().Contain("Classes=\"dialog-footer\"");
        xaml.Should().Contain("ReplayEditor.SelectedDestination");
        xaml.Should().Contain("ReplayEditor.ScheduledEnqueueTimeText");
        xaml.Should().Contain("ReplayEditor.RateLimitPerSecond");
        xaml.Should().Contain("ReplayEditor.BuildPreviewCommand");
        xaml.Should().Contain("ReplayEditor.IsConfirmed");
        xaml.Should().Contain("ReplayEditor.IsProductionAcknowledged");
        xaml.Should().Contain("ReplayEditor.ReplayCommand");
        xaml.Should().Contain("ReplayEditor.HasPreview");
    }

    [Fact]
    public void ReplayDialog_DisplaysDestinationNamespaceAndEnvironment()
    {
        // Arrange
        var xaml = File.ReadAllText(GetDialogPath());

        // Assert
        xaml.Should().Contain("SelectedDestination.NamespaceName");
        xaml.Should().Contain("SelectedDestination.Environment");
        xaml.Should().Contain("ReplayEditor.IsProductionDestination");
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
            "ReplayMessageDialog.axaml"));
    }
}
