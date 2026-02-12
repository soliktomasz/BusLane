using BusLane.ViewModels.Core;
using FluentAssertions;
using Xunit;

namespace BusLane.Tests.ViewModels.Core;

public class PaginationStateTests
{
    [Fact]
    public void CurrentPage_ShouldDefaultTo1()
    {
        // Arrange & Act
        var sut = new PaginationState();
        
        // Assert
        sut.CurrentPage.Should().Be(1);
    }
    
    [Fact]
    public void CanGoNext_WhenHasMoreMessages_ShouldBeTrue()
    {
        // Arrange
        var sut = new PaginationState();
        sut.UpdatePageInfo(1, 100, 100, true); // page 1, 100 per page, 100 messages, has more
        
        // Act & Assert
        sut.CanGoNext.Should().BeTrue();
    }
    
    [Fact]
    public void CanGoNext_WhenNoMoreMessages_ShouldBeFalse()
    {
        // Arrange
        var sut = new PaginationState();
        sut.UpdatePageInfo(1, 100, 50, false); // page 1, 100 per page, 50 messages, no more
        
        // Act & Assert
        sut.CanGoNext.Should().BeFalse();
    }
    
    [Fact]
    public void CanGoPrevious_WhenOnPage1_ShouldBeFalse()
    {
        // Arrange
        var sut = new PaginationState();
        sut.UpdatePageInfo(1, 100, 100, true);
        
        // Act & Assert
        sut.CanGoPrevious.Should().BeFalse();
    }
    
    [Fact]
    public void CanGoPrevious_WhenOnPage2_ShouldBeTrue()
    {
        // Arrange
        var sut = new PaginationState();
        sut.UpdatePageInfo(2, 100, 100, true);
        
        // Act & Assert
        sut.CanGoPrevious.Should().BeTrue();
    }
    
    [Fact]
    public void GoToNextPage_ShouldIncrementCurrentPage()
    {
        // Arrange
        var sut = new PaginationState();
        sut.UpdatePageInfo(1, 100, 100, true);
        
        // Act
        sut.GoToNextPage();
        
        // Assert
        sut.CurrentPage.Should().Be(2);
    }
    
    [Fact]
    public void GoToPreviousPage_ShouldDecrementCurrentPage()
    {
        // Arrange
        var sut = new PaginationState();
        sut.UpdatePageInfo(2, 100, 100, true);
        
        // Act
        sut.GoToPreviousPage();
        
        // Assert
        sut.CurrentPage.Should().Be(1);
    }
    
    [Fact]
    public void Reset_ShouldResetToPage1()
    {
        // Arrange
        var sut = new PaginationState();
        sut.UpdatePageInfo(3, 100, 100, true);
        
        // Act
        sut.Reset();
        
        // Assert
        sut.CurrentPage.Should().Be(1);
        sut.CanGoNext.Should().BeFalse();
        sut.CanGoPrevious.Should().BeFalse();
    }
    
    [Fact]
    public void PageInfoText_WithPartialPage_ShouldShowCorrectRange()
    {
        // Arrange
        var sut = new PaginationState();
        
        // Act
        sut.UpdatePageInfo(1, 100, 50, false); // 50 messages on page 1
        
        // Assert
        sut.PageInfoText.Should().Be("Page 1 (1-50)");
    }
    
    [Fact]
    public void PageInfoText_WithFullPage_ShouldShowCorrectRange()
    {
        // Arrange
        var sut = new PaginationState();
        
        // Act
        sut.UpdatePageInfo(1, 100, 100, true); // 100 messages on page 1
        
        // Assert
        sut.PageInfoText.Should().Be("Page 1 (1-100)");
    }
    
    [Fact]
    public void PageInfoText_OnPage2_ShouldShowCorrectRange()
    {
        // Arrange
        var sut = new PaginationState();
        
        // Act
        sut.UpdatePageInfo(2, 50, 25, false); // 25 messages on page 2 with 50 per page
        
        // Assert
        sut.PageInfoText.Should().Be("Page 2 (51-75)");
    }
}
