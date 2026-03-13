namespace BusLane.Tests.Services.Terminal;

using BusLane.Services.Terminal;
using FluentAssertions;

public class TerminalOutputBufferTests
{
    [Fact]
    public void Append_WithChunks_AppendsTextWithoutFullRefreshUntilTrim()
    {
        // Arrange
        var sut = new TerminalOutputBuffer(maxOutputLines: 2);

        // Act
        var firstUpdate = sut.Append("hello");
        var secondUpdate = sut.Append($" world{Environment.NewLine}");

        // Assert
        firstUpdate.RequiresFullRefresh.Should().BeFalse();
        firstUpdate.AppendedText.Should().Be("hello");
        secondUpdate.RequiresFullRefresh.Should().BeFalse();
        secondUpdate.AppendedText.Should().Be($" world{Environment.NewLine}");
        sut.Text.Should().Be($"hello world{Environment.NewLine}");
    }

    [Fact]
    public void Append_WhenTrimIsRequired_ReturnsFullRefreshWithTrimmedText()
    {
        // Arrange
        var sut = new TerminalOutputBuffer(maxOutputLines: 2);
        sut.Append($"one{Environment.NewLine}");
        sut.Append($"two{Environment.NewLine}");

        // Act
        var update = sut.Append($"three{Environment.NewLine}");

        // Assert
        update.RequiresFullRefresh.Should().BeTrue();
        update.Text.Should().Be($"two{Environment.NewLine}three{Environment.NewLine}");
        sut.Text.Should().Be($"two{Environment.NewLine}three{Environment.NewLine}");
    }
}
