namespace BusLane.Tests.Services.Infrastructure;

using BusLane.Services.Infrastructure;
using FluentAssertions;
using Xunit;

public class PreferencesServiceTests
{
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
}
