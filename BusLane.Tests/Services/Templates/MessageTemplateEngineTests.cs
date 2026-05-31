namespace BusLane.Tests.Services.Templates;

using BusLane.Models;
using BusLane.Services.Templates;
using FluentAssertions;

public class MessageTemplateEngineTests
{
    [Fact]
    public void ExtractTokenNames_WithTokensAcrossSendableFields_ReturnsDistinctNames()
    {
        // Arrange
        var message = new SavedMessage
        {
            Body = "{ \"order\": \"{{OrderId}}\" }",
            CorrelationId = "{{CorrelationId}}",
            Subject = "Order {{OrderId}}",
            CustomProperties = new Dictionary<string, string>
            {
                ["tenant"] = "{{TenantId}}"
            }
        };

        // Act
        var tokens = MessageTemplateEngine.ExtractTokenNames(message);

        // Assert
        tokens.Should().Equal("OrderId", "CorrelationId", "TenantId");
    }

    [Fact]
    public void FindMissingTokenValues_WithBlankValues_ReturnsMissingNames()
    {
        // Arrange
        var message = new SavedMessage
        {
            Body = "{{OrderId}}",
            CorrelationId = "{{CorrelationId}}"
        };

        var values = new Dictionary<string, string?>
        {
            ["OrderId"] = "123",
            ["CorrelationId"] = ""
        };

        // Act
        var missing = MessageTemplateEngine.FindMissingTokenValues(message, values);

        // Assert
        missing.Should().Equal("CorrelationId");
    }

    [Fact]
    public void Apply_WithValues_ReplacesTokensInAllSendableStringFields()
    {
        // Arrange
        var message = new SavedMessage
        {
            Body = "{{OrderId}}",
            ContentType = "application/{{Format}}",
            CorrelationId = "{{CorrelationId}}",
            MessageId = "{{MessageId}}",
            SessionId = "{{SessionId}}",
            Subject = "{{Subject}}",
            To = "{{To}}",
            ReplyTo = "{{ReplyTo}}",
            ReplyToSessionId = "{{ReplySession}}",
            PartitionKey = "{{PartitionKey}}",
            CustomProperties = new Dictionary<string, string>
            {
                ["tenant"] = "{{TenantId}}"
            }
        };

        var values = new Dictionary<string, string?>
        {
            ["OrderId"] = "ORD-42",
            ["Format"] = "json",
            ["CorrelationId"] = "COR-42",
            ["MessageId"] = "MSG-42",
            ["SessionId"] = "SES-42",
            ["Subject"] = "Created",
            ["To"] = "billing",
            ["ReplyTo"] = "inbox",
            ["ReplySession"] = "reply-session",
            ["PartitionKey"] = "partition",
            ["TenantId"] = "tenant-a"
        };

        // Act
        var applied = MessageTemplateEngine.Apply(message, values);

        // Assert
        applied.Body.Should().Be("ORD-42");
        applied.ContentType.Should().Be("application/json");
        applied.CorrelationId.Should().Be("COR-42");
        applied.MessageId.Should().Be("MSG-42");
        applied.SessionId.Should().Be("SES-42");
        applied.Subject.Should().Be("Created");
        applied.To.Should().Be("billing");
        applied.ReplyTo.Should().Be("inbox");
        applied.ReplyToSessionId.Should().Be("reply-session");
        applied.PartitionKey.Should().Be("partition");
        applied.CustomProperties.Should().Contain("tenant", "tenant-a");
    }
}
