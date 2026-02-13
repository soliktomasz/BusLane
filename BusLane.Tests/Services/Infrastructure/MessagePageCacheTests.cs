using BusLane.Models;
using BusLane.Services.Infrastructure;
using FluentAssertions;
using Xunit;

namespace BusLane.Tests.Services.Infrastructure;

public class MessagePageCacheTests
{
    private static MessageInfo CreateMessage(string id, string body, long sequenceNumber = 0) =>
        new(id, null, null, body, DateTimeOffset.UtcNow, null, sequenceNumber, 0, null, new Dictionary<string, object>());

    [Fact]
    public void GetPage_WhenPageNotCached_ShouldReturnEmpty()
    {
        // Arrange
        var sut = new MessagePageCache();
        
        // Act
        var result = sut.GetPage(1);
        
        // Assert
        result.Should().BeEmpty();
    }
    
    [Fact]
    public void StorePage_ShouldStoreMessages()
    {
        // Arrange
        var sut = new MessagePageCache();
        var messages = new[] { CreateMessage("1", "body1"), CreateMessage("2", "body2") };
        
        // Act
        sut.StorePage(1, messages);
        var result = sut.GetPage(1);
        
        // Assert
        result.Should().HaveCount(2);
    }
    
    [Fact]
    public void StorePage_WhenStoringPage2_ShouldNotAffectPage1()
    {
        // Arrange
        var sut = new MessagePageCache();
        var page1Messages = new[] { CreateMessage("1", "body1") };
        var page2Messages = new[] { CreateMessage("2", "body2") };
        
        // Act
        sut.StorePage(1, page1Messages);
        sut.StorePage(2, page2Messages);
        
        // Assert
        sut.GetPage(1).Should().HaveCount(1);
        sut.GetPage(2).Should().HaveCount(1);
    }
    
    [Fact]
    public void HasPage_WhenPageCached_ShouldReturnTrue()
    {
        // Arrange
        var sut = new MessagePageCache();
        sut.StorePage(1, new[] { CreateMessage("1", "body") });
        
        // Act & Assert
        sut.HasPage(1).Should().BeTrue();
    }
    
    [Fact]
    public void HasPage_WhenPageNotCached_ShouldReturnFalse()
    {
        // Arrange
        var sut = new MessagePageCache();
        
        // Act & Assert
        sut.HasPage(1).Should().BeFalse();
    }
    
    [Fact]
    public void GetLastSequenceNumber_WhenPageHasMessages_ShouldReturnLastSequenceNumber()
    {
        // Arrange
        var sut = new MessagePageCache();
        var messages = new[] 
        { 
            CreateMessage("1", "body1", 100),
            CreateMessage("2", "body2", 200)
        };
        sut.StorePage(1, messages);
        
        // Act
        var result = sut.GetLastSequenceNumber(1);
        
        // Assert
        result.Should().Be(200);
    }

    [Fact]
    public void GetMaxSequenceNumber_WhenMultiplePagesExist_ShouldReturnHighestSequenceNumber()
    {
        // Arrange
        var sut = new MessagePageCache();
        sut.StorePage(1, [CreateMessage("1", "body1", 10), CreateMessage("2", "body2", 20)]);
        sut.StorePage(2, [CreateMessage("3", "body3", 30), CreateMessage("4", "body4", 15)]);

        // Act
        var result = sut.GetMaxSequenceNumber();

        // Assert
        result.Should().Be(30);
    }

    [Fact]
    public void GetCachedSequenceNumbers_ShouldReturnDistinctSequenceNumbersFromAllPages()
    {
        // Arrange
        var sut = new MessagePageCache();
        sut.StorePage(1, [CreateMessage("1", "body1", 10), CreateMessage("2", "body2", 20)]);
        sut.StorePage(2, [CreateMessage("3", "body3", 20), CreateMessage("4", "body4", 30)]);

        // Act
        var result = sut.GetCachedSequenceNumbers();

        // Assert
        result.Should().BeEquivalentTo([10, 20, 30]);
    }
    
    [Fact]
    public void Clear_ShouldRemoveAllPages()
    {
        // Arrange
        var sut = new MessagePageCache();
        sut.StorePage(1, new[] { CreateMessage("1", "body") });
        sut.StorePage(2, new[] { CreateMessage("2", "body") });
        
        // Act
        sut.Clear();
        
        // Assert
        sut.HasPage(1).Should().BeFalse();
        sut.HasPage(2).Should().BeFalse();
    }
}
