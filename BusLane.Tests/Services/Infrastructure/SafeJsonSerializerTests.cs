using System.Text.Json;
using BusLane.Services.Infrastructure;
using FluentAssertions;

namespace BusLane.Tests.Services.Infrastructure;

public class SafeJsonSerializerTests
{
    private class TestClass
    {
        public string Name { get; set; } = "";
        public int Value { get; set; }
    }

    private struct TestStruct
    {
        public int Number { get; set; }
        public string Text { get; set; }
    }

    public class DeserializeTests
    {
        [Fact]
        public void Deserialize_WithValidJson_ReturnsDeserializedObject()
        {
            // Arrange
            var json = "{\"name\":\"test\",\"value\":42}";

            // Act
            var result = SafeJsonSerializer.Deserialize<TestClass>(json);

            // Assert
            result.Should().NotBeNull();
            result!.Name.Should().Be("test");
            result.Value.Should().Be(42);
        }

        [Fact]
        public void Deserialize_WithNullJson_ReturnsNull()
        {
            // Act
            var result = SafeJsonSerializer.Deserialize<TestClass>(null!);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public void Deserialize_WithEmptyJson_ReturnsNull()
        {
            // Act
            var result = SafeJsonSerializer.Deserialize<TestClass>("");

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public void Deserialize_WithDocumentExceeding10MB_ThrowsArgumentException()
        {
            // Arrange
            var largeJson = new string('a', 11 * 1024 * 1024); // 11MB of characters

            // Act
            var act = () => SafeJsonSerializer.Deserialize<TestClass>(largeJson);

            // Assert
            act.Should().Throw<ArgumentException>()
                .WithMessage("*exceeds the maximum allowed size*");
        }

        [Fact]
        public void Deserialize_WithDocumentAt10MBLimit_DoesNotThrow()
        {
            // Arrange - create a valid JSON object close to but under 10MB
            // Using a string that's approximately 9MB (accounting for UTF-8 encoding)
            var largeString = new string('a', 9 * 1024 * 1024);
            var json = $"{{\"name\":\"{largeString}\",\"value\":1}}";

            // Act
            var act = () => SafeJsonSerializer.Deserialize<TestClass>(json);

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public void Deserialize_WithInvalidJson_ThrowsJsonException()
        {
            // Arrange
            var invalidJson = "{invalid json}";

            // Act
            var act = () => SafeJsonSerializer.Deserialize<TestClass>(invalidJson);

            // Assert
            act.Should().Throw<JsonException>();
        }

        [Fact]
        public void Deserialize_WithCamelCaseNamingPolicy_DeserializesCorrectly()
        {
            // Arrange
            var json = "{\"name\":\"test\",\"value\":42}";

            // Act
            var result = SafeJsonSerializer.Deserialize<TestClass>(json);

            // Assert
            result.Should().NotBeNull();
            result!.Name.Should().Be("test");
        }

        [Fact]
        public void Deserialize_WithComments_SkipsComments()
        {
            // Arrange
            var json = @"{
                // This is a comment
                ""name"": ""test"",
                ""value"": 42
            }";

            // Act
            var result = SafeJsonSerializer.Deserialize<TestClass>(json);

            // Assert
            result.Should().NotBeNull();
            result!.Name.Should().Be("test");
        }
    }

    public class DeserializeValueTests
    {
        [Fact]
        public void DeserializeValue_WithValidJson_ReturnsDeserializedValue()
        {
            // Arrange
            var json = "{\"number\":123,\"text\":\"hello\"}";

            // Act
            var result = SafeJsonSerializer.DeserializeValue<TestStruct>(json);

            // Assert
            result.Should().NotBeNull();
            result!.Value.Number.Should().Be(123);
            result.Value.Text.Should().Be("hello");
        }

        [Fact]
        public void DeserializeValue_WithNullJson_ReturnsNull()
        {
            // Act
            var result = SafeJsonSerializer.DeserializeValue<int>(null!);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public void DeserializeValue_WithDocumentExceeding10MB_ThrowsArgumentException()
        {
            // Arrange
            var largeJson = new string('a', 11 * 1024 * 1024);

            // Act
            var act = () => SafeJsonSerializer.DeserializeValue<TestStruct>(largeJson);

            // Assert
            act.Should().Throw<ArgumentException>()
                .WithMessage("*exceeds the maximum allowed size*");
        }
    }

    public class DeserializeListTests
    {
        [Fact]
        public void DeserializeList_WithValidJson_ReturnsDeserializedList()
        {
            // Arrange
            var json = "[{\"name\":\"item1\",\"value\":1},{\"name\":\"item2\",\"value\":2}]";

            // Act
            var result = SafeJsonSerializer.DeserializeList<TestClass>(json);

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(2);
            result[0].Name.Should().Be("item1");
            result[1].Name.Should().Be("item2");
        }

        [Fact]
        public void DeserializeList_WithNullJson_ReturnsEmptyList()
        {
            // Act
            var result = SafeJsonSerializer.DeserializeList<TestClass>(null!);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEmpty();
        }

        [Fact]
        public void DeserializeList_WithEmptyJson_ReturnsEmptyList()
        {
            // Act
            var result = SafeJsonSerializer.DeserializeList<TestClass>("");

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEmpty();
        }

        [Fact]
        public void DeserializeList_WithDocumentExceeding10MB_ThrowsArgumentException()
        {
            // Arrange
            var largeJson = new string('a', 11 * 1024 * 1024);

            // Act
            var act = () => SafeJsonSerializer.DeserializeList<TestClass>(largeJson);

            // Assert
            act.Should().Throw<ArgumentException>()
                .WithMessage("*exceeds the maximum allowed size*");
        }

        [Fact]
        public void DeserializeList_WithInvalidJson_ThrowsJsonException()
        {
            // Arrange
            var invalidJson = "{invalid json}";

            // Act
            var act = () => SafeJsonSerializer.DeserializeList<TestClass>(invalidJson);

            // Assert
            act.Should().Throw<JsonException>();
        }
    }

    public class SerializeTests
    {
        [Fact]
        public void Serialize_WithValidObject_ReturnsJsonString()
        {
            // Arrange
            var obj = new TestClass { Name = "test", Value = 42 };

            // Act
            var result = SafeJsonSerializer.Serialize(obj);

            // Assert
            result.Should().NotBeNullOrEmpty();
            result.Should().Contain("\"name\": \"test\"");
            result.Should().Contain("\"value\": 42");
        }

        [Fact]
        public void Serialize_WithNullObject_ThrowsArgumentNullException()
        {
            // Act
            var act = () => SafeJsonSerializer.Serialize<TestClass>(null!);

            // Assert
            act.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void Serialize_WithCustomOptions_UsesCustomOptions()
        {
            // Arrange
            var obj = new TestClass { Name = "test", Value = 42 };
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = null, // Use exact property names
                WriteIndented = false
            };

            // Act
            var result = SafeJsonSerializer.Serialize(obj, options);

            // Assert
            result.Should().Contain("\"Name\"");
            result.Should().NotContain("\"name\"");
        }

        [Fact]
        public void Serialize_WithWriteIndented_FormatsOutput()
        {
            // Arrange
            var obj = new TestClass { Name = "test", Value = 42 };

            // Act
            var result = SafeJsonSerializer.Serialize(obj);

            // Assert
            result.Should().Contain("\n");
            result.Should().Contain("  ");
        }
    }

    public class IntegrationTests
    {
        [Fact]
        public void RoundTrip_SerializeThenDeserialize_PreservesData()
        {
            // Arrange
            var original = new TestClass { Name = "integration test", Value = 999 };

            // Act
            var json = SafeJsonSerializer.Serialize(original);
            var deserialized = SafeJsonSerializer.Deserialize<TestClass>(json);

            // Assert
            deserialized.Should().NotBeNull();
            deserialized!.Name.Should().Be(original.Name);
            deserialized.Value.Should().Be(original.Value);
        }

        [Fact]
        public void RoundTrip_SerializeThenDeserializeList_PreservesData()
        {
            // Arrange
            var original = new List<TestClass>
            {
                new() { Name = "item1", Value = 1 },
                new() { Name = "item2", Value = 2 }
            };

            // Act
            var json = SafeJsonSerializer.Serialize(original);
            var deserialized = SafeJsonSerializer.DeserializeList<TestClass>(json);

            // Assert
            deserialized.Should().HaveCount(2);
            deserialized[0].Name.Should().Be("item1");
            deserialized[1].Name.Should().Be("item2");
        }

        [Fact]
        public void Deserialize_WithLargeDocumentCloseToLimit_Succeeds()
        {
            // Arrange - Create a document that's approximately 9MB
            // which should be under the 10MB limit
            var largeString = new string('x', 8 * 1024 * 1024);
            var obj = new TestClass { Name = largeString, Value = 1 };
            var json = SafeJsonSerializer.Serialize(obj);

            // Act
            var result = SafeJsonSerializer.Deserialize<TestClass>(json);

            // Assert
            result.Should().NotBeNull();
            result!.Name.Length.Should().Be(largeString.Length);
        }
    }
}
