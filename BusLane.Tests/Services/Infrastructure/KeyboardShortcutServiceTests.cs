namespace BusLane.Tests.Services.Infrastructure;

using Avalonia.Input;
using BusLane.Services.Infrastructure;
using FluentAssertions;

public class KeyboardShortcutServiceTests
{
    [Fact]
    public void GetGesture_ForToggleTerminal_ReturnsExpectedShortcut()
    {
        // Arrange
        var sut = new KeyboardShortcutService();

        // Act
        var gesture = sut.GetGesture(KeyboardShortcutAction.ToggleTerminal);

        // Assert
        gesture.Should().NotBeNull();
        gesture!.Key.Should().Be(Key.T);
        gesture.KeyModifiers.Should().Be(sut.PrimaryModifier | KeyModifiers.Shift);
    }
}
