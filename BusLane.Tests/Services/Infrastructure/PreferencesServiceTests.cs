namespace BusLane.Tests.Services.Infrastructure;

using BusLane.Services.Infrastructure;
using FluentAssertions;
using Xunit;

public class PreferencesServiceTests : IDisposable
{
    private readonly string _preferencesPath;
    private readonly string? _backupPath;

    public PreferencesServiceTests()
    {
        _preferencesPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "BusLane",
            "preferences.json");

        if (File.Exists(_preferencesPath))
        {
            _backupPath = $"{_preferencesPath}.bak-{Guid.NewGuid():N}";
            Directory.CreateDirectory(Path.GetDirectoryName(_backupPath)!);
            File.Move(_preferencesPath, _backupPath);
        }

        if (File.Exists(_preferencesPath))
        {
            File.Delete(_preferencesPath);
        }
    }

    public void Dispose()
    {
        if (File.Exists(_preferencesPath))
        {
            try { File.Delete(_preferencesPath); } catch { }
        }

        if (!string.IsNullOrWhiteSpace(_backupPath) && File.Exists(_backupPath))
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_preferencesPath)!);
                File.Move(_backupPath, _preferencesPath);
            }
            catch
            {
                // Ignore restore issues in test cleanup.
            }
        }
    }

    [Fact]
    public void MessagesPerPage_ShouldDefaultTo100()
    {
        // Arrange
        var sut = new PreferencesService();
        
        // Act
        var result = sut.MessagesPerPage;
        
        // Assert
        result.Should().Be(100);
    }
    
    [Fact]
    public void MaxTotalMessages_ShouldDefaultTo500()
    {
        // Arrange
        var sut = new PreferencesService();
        
        // Act
        var result = sut.MaxTotalMessages;
        
        // Assert
        result.Should().Be(500);
    }

    [Fact]
    public void TerminalPreferences_ShouldHaveExpectedDefaults()
    {
        // Arrange
        var sut = new PreferencesService();

        // Assert
        sut.ShowTerminalPanel.Should().BeFalse();
        sut.TerminalIsDocked.Should().BeTrue();
        sut.TerminalDockHeight.Should().Be(260);
        sut.TerminalWindowBoundsJson.Should().BeNull();
    }

    [Fact]
    public void SaveAndReload_ShouldRoundTripTerminalPreferences()
    {
        // Arrange
        var sut = new PreferencesService
        {
            ShowTerminalPanel = true,
            TerminalIsDocked = false,
            TerminalDockHeight = 320,
            TerminalWindowBoundsJson = "{\"X\":120,\"Y\":140,\"Width\":900,\"Height\":420}"
        };

        // Act
        sut.Save();
        var reloaded = new PreferencesService();

        // Assert
        reloaded.ShowTerminalPanel.Should().BeTrue();
        reloaded.TerminalIsDocked.Should().BeFalse();
        reloaded.TerminalDockHeight.Should().Be(320);
        reloaded.TerminalWindowBoundsJson.Should().Be("{\"X\":120,\"Y\":140,\"Width\":900,\"Height\":420}");
    }
}
