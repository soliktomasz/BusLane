namespace BusLane.Tests.Services.ServiceBus;

using BusLane.Models;
using BusLane.Services.ServiceBus;
using FluentAssertions;
using Xunit;

public class ScheduledMessageStoreTests
{
    [Fact]
    public async Task LoadAsync_WhenCanceled_PropagatesCancellation()
    {
        // Arrange
        var path = Path.Combine(Path.GetTempPath(), $"buslane-scheduled-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(path, "[]");
        var sut = new ScheduledMessageStore(path);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        try
        {
            // Act
            var act = () => sut.LoadAsync(cts.Token);

            // Assert
            await act.Should().ThrowAsync<OperationCanceledException>();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task RemoveAsync_WithBlankEntityName_Throws(string? entityName)
    {
        // Arrange
        var path = Path.Combine(Path.GetTempPath(), $"buslane-scheduled-{Guid.NewGuid():N}.json");
        var sut = new ScheduledMessageStore(path);

        // Act
        var act = () => sut.RemoveAsync(entityName!, 42);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("entityName");
    }

    [Fact]
    public async Task AddAsync_CreatesStoreFileWithOwnerOnlyPermissionsOnUnix()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        // Arrange
        var directory = Path.Combine(Path.GetTempPath(), $"buslane-scheduled-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "scheduled.json");
        var sut = new ScheduledMessageStore(path);

        try
        {
            // Act
            await sut.AddAsync(CreateEntry("orders", 42));

            // Assert
            File.GetUnixFileMode(path).Should().Be(UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private static ScheduledMessageIndexEntry CreateEntry(string entityName, long sequenceNumber) =>
        new(
            entityName,
            null,
            sequenceNumber,
            DateTimeOffset.UtcNow.AddHours(1),
            null,
            "body",
            DateTimeOffset.UtcNow);
}
