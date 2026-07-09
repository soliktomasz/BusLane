namespace BusLane.Tests.ViewModels;

using BusLane.Models;
using BusLane.Services.ServiceBus;
using BusLane.ViewModels;
using FluentAssertions;
using NSubstitute;

public class SendMessageViewModelTests
{
    private readonly IServiceBusOperations _operations = Substitute.For<IServiceBusOperations>();
    private readonly string _savedMessagesPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "saved_messages.json");
    private string? _statusMessage;
    private bool _closed;

    [Fact]
    public void SaveMessage_WithCategoryAndTags_PersistsTemplateMetadata()
    {
        // Arrange
        var sut = CreateSut();
        sut.Body = "{{OrderId}}";
        sut.SaveMessageName = "Create Order";
        sut.SaveMessageCategory = "Orders";
        sut.SaveMessageTags = "smoke, billing";

        // Act
        sut.SaveMessageCommand.Execute(null);

        // Assert
        sut.SavedMessages.Should().ContainSingle();
        var saved = sut.SavedMessages.Single();
        saved.Category.Should().Be("Orders");
        saved.Tags.Should().Equal("smoke", "billing");
    }

    [Fact]
    public void FilteredSavedMessages_WithSearchQuery_MatchesNameCategoryTagsAndPayload()
    {
        // Arrange
        var sut = CreateSut();
        sut.SavedMessages.Add(new SavedMessage
        {
            Name = "Create Order",
            Category = "Orders",
            Tags = new List<string> { "billing" },
            Body = "payload"
        });
        sut.SavedMessages.Add(new SavedMessage
        {
            Name = "Ping",
            Category = "Health",
            Body = "ready"
        });

        // Act
        sut.TemplateSearchQuery = "billing";

        // Assert
        sut.FilteredSavedMessages.Should().ContainSingle()
            .Which.Name.Should().Be("Create Order");
    }

    [Fact]
    public void LoadMessage_WithParameterizedTemplate_PopulatesTokenValues()
    {
        // Arrange
        var sut = CreateSut();
        var template = new SavedMessage
        {
            Body = "{{OrderId}}",
            CorrelationId = "{{CorrelationId}}",
            TokenValues = new Dictionary<string, string> { { "OrderId", "ORD-1" } }
        };

        // Act
        sut.LoadMessageCommand.Execute(template);

        // Assert
        sut.TemplateTokenValues.Should().HaveCount(2);
        sut.TemplateTokenValues.Should().Contain(t => t.Name == "OrderId" && t.Value == "ORD-1");
        sut.TemplateTokenValues.Should().Contain(t => t.Name == "CorrelationId" && t.Value == "");
    }

    [Fact]
    public async Task SendAsync_WithMissingTemplateValue_SetsErrorAndDoesNotSend()
    {
        // Arrange
        var sut = CreateSut();
        sut.LoadMessageCommand.Execute(new SavedMessage
        {
            Body = "{{OrderId}}",
            CorrelationId = "{{CorrelationId}}"
        });
        sut.TemplateTokenValues.Single(t => t.Name == "OrderId").Value = "ORD-1";

        // Act
        await sut.SendCommand.ExecuteAsync(null);

        // Assert
        sut.ErrorMessage.Should().Be("Missing template values: CorrelationId");
        await _operations.DidNotReceiveWithAnyArgs().SendMessageAsync(default!, default!, default);
    }

    [Fact]
    public async Task SendAsync_WithTemplateValues_SendsAppliedMessage()
    {
        // Arrange
        var sut = CreateSut();
        sut.LoadMessageCommand.Execute(new SavedMessage
        {
            Body = "{{OrderId}}",
            ContentType = "application/json",
            CorrelationId = "{{CorrelationId}}",
            CustomProperties = new Dictionary<string, string>
            {
                ["tenant"] = "{{TenantId}}"
            }
        });
        sut.TemplateTokenValues.Single(t => t.Name == "OrderId").Value = "ORD-1";
        sut.TemplateTokenValues.Single(t => t.Name == "CorrelationId").Value = "COR-1";
        sut.TemplateTokenValues.Single(t => t.Name == "TenantId").Value = "tenant-a";

        // Act
        await sut.SendCommand.ExecuteAsync(null);

        // Assert
        await _operations.Received(1).SendMessageAsync(
            "queue",
            "ORD-1",
            Arg.Is<IDictionary<string, object>>(p => p["tenant"].Equals("tenant-a")),
            "application/json",
            "COR-1");
        _closed.Should().BeTrue();
        _statusMessage.Should().Be("Message sent successfully");
    }

    [Fact]
    public async Task SendAsync_WithScheduledEnqueueTime_UsesScheduleApiAndRecordsSequenceNumber()
    {
        // Arrange
        var scheduledAt = DateTimeOffset.UtcNow.AddHours(1);
        _operations.ScheduleMessageAsync(
                "queue",
                Arg.Any<string>(),
                Arg.Any<IDictionary<string, object>>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<TimeSpan?>(),
                Arg.Any<CancellationToken>())
            .Returns(42);
        var store = Substitute.For<IScheduledMessageStore>();
        var sut = CreateSut(store);
        sut.Body = "{\"id\":1}";
        sut.ScheduledEnqueueTimeText = scheduledAt.ToString("O");

        // Act
        await sut.SendCommand.ExecuteAsync(null);

        // Assert
        await _operations.Received(1).ScheduleMessageAsync(
            "queue",
            "{\"id\":1}",
            Arg.Any<IDictionary<string, object>>(),
            Arg.Is<DateTimeOffset>(value => value == scheduledAt),
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<TimeSpan?>(),
            Arg.Any<CancellationToken>());
        await _operations.DidNotReceiveWithAnyArgs().SendMessageAsync(default!, default!, default);
        await store.Received(1).AddAsync(Arg.Is<ScheduledMessageIndexEntry>(entry =>
            entry.EntityName == "queue" &&
            entry.SequenceNumber == 42 &&
            entry.ScheduledEnqueueTime == scheduledAt &&
            entry.BodyPreview == "{\"id\":1}"));
        _statusMessage.Should().Be("Message scheduled successfully (sequence 42)");
    }

    [Fact]
    public async Task SendAsync_WhenScheduledIndexWriteFails_StillReportsScheduledMessage()
    {
        // Arrange
        var scheduledAt = DateTimeOffset.UtcNow.AddHours(1);
        _operations.ScheduleMessageAsync(
                "queue",
                Arg.Any<string>(),
                Arg.Any<IDictionary<string, object>>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<TimeSpan?>(),
                Arg.Any<CancellationToken>())
            .Returns(42);
        var store = Substitute.For<IScheduledMessageStore>();
        store.AddAsync(Arg.Any<ScheduledMessageIndexEntry>(), Arg.Any<CancellationToken>())
            .Returns(_ => throw new IOException("disk full"));
        var sut = CreateSut(store);
        sut.Body = "{\"id\":1}";
        sut.ScheduledEnqueueTimeText = scheduledAt.ToString("O");

        // Act
        await sut.SendCommand.ExecuteAsync(null);

        // Assert
        sut.ErrorMessage.Should().BeNull();
        _closed.Should().BeTrue();
        _statusMessage.Should().Be("Message scheduled successfully (sequence 42)");
    }

    [Fact]
    public void DuplicateSavedMessage_CopiesTemplateWithNewName()
    {
        // Arrange
        var sut = CreateSut();
        var template = new SavedMessage
        {
            Name = "Create Order",
            Body = "{{OrderId}}",
            Category = "Orders",
            Tags = new List<string> { "billing" }
        };
        sut.SavedMessages.Add(template);

        // Act
        sut.DuplicateSavedMessageCommand.Execute(template);

        // Assert
        sut.SavedMessages.Should().HaveCount(2);
        var duplicate = sut.SavedMessages.Last();
        duplicate.Name.Should().Be("Create Order Copy");
        duplicate.Category.Should().Be("Orders");
        duplicate.Tags.Should().Equal("billing");
    }

    [Fact]
    public void UpdateActiveTemplate_WithLoadedTemplate_EditsExistingTemplate()
    {
        // Arrange
        var sut = CreateSut();
        var template = new SavedMessage
        {
            Name = "Create Order",
            Body = "{{OrderId}}",
            Category = "Orders"
        };
        sut.SavedMessages.Add(template);
        sut.LoadMessageCommand.Execute(template);
        sut.Body = "{{OrderId}}-updated";
        sut.TemplateTokenValues.Single(t => t.Name == "OrderId").Value = "ORD-2";

        // Act
        sut.UpdateActiveTemplateCommand.Execute(null);

        // Assert
        sut.SavedMessages.Should().ContainSingle();
        template.Body.Should().Be("{{OrderId}}-updated");
        template.TokenValues.Should().Contain("OrderId", "ORD-2");
        template.Category.Should().Be("Orders");
    }

    private SendMessageViewModel CreateSut(IScheduledMessageStore? scheduledMessageStore = null)
    {
        return new SendMessageViewModel(
            _operations,
            "queue",
            () => _closed = true,
            message => _statusMessage = message,
            savedMessagesPath: _savedMessagesPath,
            scheduledMessageStore: scheduledMessageStore);
    }
}
