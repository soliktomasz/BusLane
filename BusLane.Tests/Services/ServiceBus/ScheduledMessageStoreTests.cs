namespace BusLane.Tests.Services.ServiceBus;

using BusLane.Models;
using BusLane.Services.Infrastructure;
using BusLane.Services.ServiceBus;
using FluentAssertions;
using NSubstitute;
using Xunit;

public class ScheduledMessageStoreTests
{
    [Fact]
    public async Task AddAsync_WithPayload_PersistsEncryptedPayloadWithoutPlainBody()
    {
        var path = Path.Combine(Path.GetTempPath(), $"buslane-scheduled-{Guid.NewGuid():N}.json");
        var encryption = Substitute.For<IEncryptionService>();
        encryption.Encrypt(Arg.Any<string>()).Returns(call => $"enc:{Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(call.Arg<string>()))}");
        var sut = new ScheduledMessageStore(encryption, TimeProvider.System, path);
        var payload = new ScheduledMessagePayload(
            "secret body", "application/json", null, "message-1", null, null, null, null,
            null, null, null,
            new Dictionary<string, ScheduledMessagePropertyValue>
            {
                ["tenant"] = new("String", "north")
            });

        try
        {
            await sut.AddAsync(CreateEntry("orders", 42), payload);

            var raw = await File.ReadAllTextAsync(path);
            raw.Should().NotContain("secret body");
            raw.Should().NotContain("north");
            raw.Should().NotContain("\"body\"");
            raw.Should().Contain("enc:");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task LoadAsync_WithLegacyRecord_ReturnsLimitedEntry()
    {
        var path = Path.Combine(Path.GetTempPath(), $"buslane-scheduled-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(path, """
            [{"EntityName":"orders","SubscriptionName":null,"SequenceNumber":42,
              "ScheduledEnqueueTime":"2026-08-01T10:00:00+00:00","MessageId":"m1",
              "BodyPreview":"body","CreatedAt":"2026-07-29T10:00:00+00:00"}]
            """);
        var sut = new ScheduledMessageStore(path);

        try
        {
            var entry = (await sut.LoadAsync()).Single();
            entry.SchemaVersion.Should().Be(1);
            entry.IsLegacyLimited.Should().BeTrue();
            entry.RecordId.Should().Be("orders:42");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task LoadAsync_WithCurrentSchemaRecord_PreservesCurrentSchema()
    {
        var path = Path.Combine(Path.GetTempPath(), $"buslane-scheduled-{Guid.NewGuid():N}.json");
        var encryption = Substitute.For<IEncryptionService>();
        encryption.Decrypt("encrypted").Returns("{}");
        var sut = new ScheduledMessageStore(encryption, TimeProvider.System, path);
        var current = new ScheduledMessageIndexEntry
        {
            RecordId = "record-1",
            EntityName = "orders",
            SequenceNumber = 42,
            ScheduledEnqueueTime = DateTimeOffset.UtcNow.AddHours(1),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            EncryptedPayload = "encrypted"
        };

        try
        {
            await sut.AddAsync(current);

            var loaded = (await sut.LoadAsync()).Single();
            loaded.SchemaVersion.Should().Be(ScheduledMessageIndexEntry.CurrentSchemaVersion);
            loaded.RecordId.Should().Be("record-1");
            loaded.IsLegacyLimited.Should().BeFalse();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task UpdateAsync_ConcurrentMutations_DoNotLoseEntries()
    {
        var path = Path.Combine(Path.GetTempPath(), $"buslane-scheduled-{Guid.NewGuid():N}.json");
        var sut = new ScheduledMessageStore(path);

        try
        {
            await Task.WhenAll(Enumerable.Range(1, 20)
                .Select(i => sut.AddAsync(CreateEntry("orders", i))));

            (await sut.LoadAsync()).Should().HaveCount(20);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task LoadAsync_WithUndecryptablePayload_ReturnsStaleLimitedEntry()
    {
        var path = Path.Combine(Path.GetTempPath(), $"buslane-scheduled-{Guid.NewGuid():N}.json");
        var encryption = Substitute.For<IEncryptionService>();
        encryption.Encrypt(Arg.Any<string>()).Returns("encrypted");
        encryption.Decrypt("encrypted").Returns((string?)null);
        var sut = new ScheduledMessageStore(encryption, TimeProvider.System, path);

        try
        {
            await sut.AddAsync(CreateEntry("orders", 42), new ScheduledMessagePayload(
                "body", null, null, null, null, null, null, null, null, null, null,
                new Dictionary<string, ScheduledMessagePropertyValue>()));
            var entry = (await sut.LoadAsync()).Single();

            (await sut.LoadPayloadAsync(entry)).Should().BeNull();
            entry.IsLegacyLimited.Should().BeTrue();
            (await sut.LoadAsync()).Should().ContainSingle();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task AddAsync_WithUnreadableStore_DoesNotOverwriteExistingContent()
    {
        var path = Path.Combine(Path.GetTempPath(), $"buslane-scheduled-{Guid.NewGuid():N}.json");
        const string corrupt = "{not-json";
        await File.WriteAllTextAsync(path, corrupt);
        var sut = new ScheduledMessageStore(path);

        try
        {
            var act = () => sut.AddAsync(CreateEntry("orders", 42));
            await act.Should().ThrowAsync<InvalidDataException>();
            (await File.ReadAllTextAsync(path)).Should().Be(corrupt);
        }
        finally
        {
            File.Delete(path);
        }
    }

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
