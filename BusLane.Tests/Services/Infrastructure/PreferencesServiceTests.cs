namespace BusLane.Tests.Services.Infrastructure;

using BusLane.Services.Infrastructure;
using FluentAssertions;
using Xunit;

public class PreferencesServiceTests : IDisposable
{
    private readonly string _preferencesPath;
    private readonly string _testDirectory;

    public PreferencesServiceTests()
    {
        _testDirectory = Directory.CreateTempSubdirectory("BusLane-PreferencesTests-").FullName;
        _preferencesPath = Path.Combine(_testDirectory, "preferences.json");
    }

    public void Dispose()
    {
        try { Directory.Delete(_testDirectory, recursive: true); } catch { }
    }

    [Fact]
    public void MessagesPerPage_ShouldDefaultTo100()
    {
        // Arrange
        var _sut = new PreferencesService(_preferencesPath);
        
        // Act
        var result = _sut.MessagesPerPage;
        
        // Assert
        result.Should().Be(100);
    }
    
    [Fact]
    public void TerminalPreferences_ShouldHaveExpectedDefaults()
    {
        // Arrange
        var _sut = new PreferencesService(_preferencesPath);

        // Assert
        _sut.HasSeenIntroduction.Should().BeFalse();
        _sut.ShowTopicActionButtons.Should().BeTrue();
        _sut.ShowTerminalPanel.Should().BeFalse();
        _sut.TerminalIsDocked.Should().BeTrue();
        _sut.TerminalDockHeight.Should().Be(260);
        _sut.TerminalWindowBoundsJson.Should().BeNull();
    }

    [Fact]
    public void SaveAndReload_ShouldRoundTripTerminalPreferences()
    {
        // Arrange
        var _sut = new PreferencesService(_preferencesPath)
        {
            ShowTerminalPanel = true,
            TerminalIsDocked = false,
            TerminalDockHeight = 320,
            TerminalWindowBoundsJson = "{\"X\":120,\"Y\":140,\"Width\":900,\"Height\":420}"
        };

        // Act
        _sut.Save();
        var reloaded = new PreferencesService(_preferencesPath);

        // Assert
        reloaded.ShowTerminalPanel.Should().BeTrue();
        reloaded.TerminalIsDocked.Should().BeFalse();
        reloaded.TerminalDockHeight.Should().Be(320);
        reloaded.TerminalWindowBoundsJson.Should().Be("{\"X\":120,\"Y\":140,\"Width\":900,\"Height\":420}");
    }

    [Fact]
    public void SaveAndReload_ShouldRoundTripPinnedEntitiesJson()
    {
        // Arrange
        var _sut = new PreferencesService(_preferencesPath)
        {
            PinnedEntitiesJson = """
                [{"WorkspaceId":"workspace-a","Type":"Queue","Name":"orders","TopicName":null}]
                """
        };

        // Act
        _sut.Save();
        var reloaded = new PreferencesService(_preferencesPath);

        // Assert
        reloaded.PinnedEntitiesJson.Should().Contain("workspace-a");
        reloaded.PinnedEntitiesJson.Should().Contain("orders");
    }

    [Fact]
    public void SaveAndReload_ShouldRoundTripTopicActionButtonVisibility()
    {
        // Arrange
        var _sut = new PreferencesService(_preferencesPath)
        {
            ShowTopicActionButtons = false
        };

        // Act
        _sut.Save();
        var reloaded = new PreferencesService(_preferencesPath);

        // Assert
        reloaded.ShowTopicActionButtons.Should().BeFalse();
    }

    [Fact]
    public void SaveAndReload_ShouldRoundTripIntroductionPreference()
    {
        // Arrange
        var _sut = new PreferencesService(_preferencesPath)
        {
            HasSeenIntroduction = true
        };

        // Act
        _sut.Save();
        var reloaded = new PreferencesService(_preferencesPath);

        // Assert
        reloaded.HasSeenIntroduction.Should().BeTrue();
    }
}
