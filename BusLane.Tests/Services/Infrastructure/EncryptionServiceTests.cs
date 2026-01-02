using BusLane.Services.Infrastructure;
using FluentAssertions;

namespace BusLane.Tests.Services.Infrastructure;

public class EncryptionServiceTests
{
    private readonly EncryptionService _sut;

    public EncryptionServiceTests()
    {
        _sut = new EncryptionService();
    }

    [Fact]
    public void Encrypt_WithValidText_ReturnsEncryptedStringWithPrefix()
    {
        // Arrange
        const string plainText = "my-secret-connection-string";

        // Act
        var encrypted = _sut.Encrypt(plainText);

        // Assert
        encrypted.Should().StartWith("ENC:");
        encrypted.Should().NotBe(plainText);
    }

    [Fact]
    public void Encrypt_WithSameText_ReturnsDifferentEncryptedStrings()
    {
        // Arrange - Each encryption uses a random IV, so results should differ
        const string plainText = "my-secret-connection-string";

        // Act
        var encrypted1 = _sut.Encrypt(plainText);
        var encrypted2 = _sut.Encrypt(plainText);

        // Assert
        encrypted1.Should().NotBe(encrypted2);
    }

    [Fact]
    public void Decrypt_WithEncryptedText_ReturnsOriginalText()
    {
        // Arrange
        const string originalText = "Endpoint=sb://mybus.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=abc123";
        var encrypted = _sut.Encrypt(originalText);

        // Act
        var decrypted = _sut.Decrypt(encrypted);

        // Assert
        decrypted.Should().Be(originalText);
    }

    [Fact]
    public void Decrypt_WithUnencryptedText_ReturnsOriginalText()
    {
        // Arrange - For backward compatibility, unencrypted strings should pass through
        const string plainText = "Endpoint=sb://mybus.servicebus.windows.net/";

        // Act
        var result = _sut.Decrypt(plainText);

        // Assert
        result.Should().Be(plainText);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Encrypt_WithNullOrEmpty_ReturnsInputUnchanged(string? input)
    {
        // Act
        var result = _sut.Encrypt(input!);

        // Assert
        result.Should().Be(input);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Decrypt_WithNullOrEmpty_ReturnsInputUnchanged(string? input)
    {
        // Act
        var result = _sut.Decrypt(input!);

        // Assert
        result.Should().Be(input);
    }

    [Fact]
    public void IsEncrypted_WithEncryptedText_ReturnsTrue()
    {
        // Arrange
        var encrypted = _sut.Encrypt("test");

        // Act
        var result = _sut.IsEncrypted(encrypted);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsEncrypted_WithPlainText_ReturnsFalse()
    {
        // Arrange
        const string plainText = "not-encrypted-string";

        // Act
        var result = _sut.IsEncrypted(plainText);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void IsEncrypted_WithNullOrEmpty_ReturnsFalse(string? input)
    {
        // Act
        var result = _sut.IsEncrypted(input!);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Decrypt_WithInvalidBase64_ReturnsNull()
    {
        // Arrange - ENC: prefix but invalid base64
        const string invalidEncrypted = "ENC:not-valid-base64!!!";

        // Act
        var result = _sut.Decrypt(invalidEncrypted);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void Decrypt_WithTooShortData_ReturnsNull()
    {
        // Arrange - Valid prefix and base64, but too short for salt + IV + data
        var tooShort = "ENC:" + Convert.ToBase64String(new byte[10]);

        // Act
        var result = _sut.Decrypt(tooShort);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void EncryptDecrypt_WithSpecialCharacters_PreservesContent()
    {
        // Arrange
        const string specialChars = "こんにちは🎉\n\t\"quoted\"";

        // Act
        var encrypted = _sut.Encrypt(specialChars);
        var decrypted = _sut.Decrypt(encrypted);

        // Assert
        decrypted.Should().Be(specialChars);
    }

    [Fact]
    public void EncryptDecrypt_WithLongText_PreservesContent()
    {
        // Arrange
        var longText = new string('A', 10000);

        // Act
        var encrypted = _sut.Encrypt(longText);
        var decrypted = _sut.Decrypt(encrypted);

        // Assert
        decrypted.Should().Be(longText);
    }
}

