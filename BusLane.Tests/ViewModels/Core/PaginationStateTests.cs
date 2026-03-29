namespace BusLane.Tests.ViewModels.Core;

using BusLane.ViewModels.Core;
using FluentAssertions;
using Xunit;

public class PaginationStateTests
{
    [Fact]
    public void CurrentPage_ShouldDefaultTo1()
    {
        // Arrange & Act
        var sut = new PaginationState();
        
        // Assert
        sut.CurrentPage.Should().Be(1);
        sut.HasPageInfo.Should().BeFalse();
        sut.PageLabel.Should().BeEmpty();
        sut.PageRangeText.Should().BeEmpty();
        sut.PageDetailText.Should().BeNull();
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
        sut.HasPageInfo.Should().BeFalse();
        sut.PageLabel.Should().BeEmpty();
        sut.PageRangeText.Should().BeEmpty();
        sut.PageDetailText.Should().BeNull();
    }
    
    [Fact]
    public void UpdatePageInfo_WithoutKnownTotal_ShouldExposeStructuredRangeOnly()
    {
        // Arrange
        var sut = new PaginationState();
        
        // Act
        sut.UpdatePageInfo(1, 100, 50, false); // 50 messages on page 1
        
        // Assert
        sut.HasPageInfo.Should().BeTrue();
        sut.PageLabel.Should().Be("Page 1");
        sut.PageRangeText.Should().Be("Showing 1-50");
        sut.PageDetailText.Should().BeNull();
    }
    
    [Fact]
    public void UpdatePageInfoWithTotal_WithKnownTotal_ShouldExposeStructuredLabels()
    {
        // Arrange
        var sut = new PaginationState();
        
        // Act
        sut.UpdatePageInfoWithTotal(2, 50, 75);
        
        // Assert
        sut.HasPageInfo.Should().BeTrue();
        sut.PageLabel.Should().Be("Page 2");
        sut.PageRangeText.Should().Be("Showing 51-75");
        sut.PageDetailText.Should().Be("of 75 messages");
    }
    
    [Fact]
    public void UpdatePageInfoWithTotal_WhenAnotherPageExists_ShouldKeepNextEnabled()
    {
        // Arrange
        var sut = new PaginationState();
        
        // Act
        sut.UpdatePageInfoWithTotal(2, 50, 140);
        
        // Assert
        sut.CanGoPrevious.Should().BeTrue();
        sut.CanGoNext.Should().BeTrue();
        sut.PageLabel.Should().Be("Page 2");
        sut.PageRangeText.Should().Be("Showing 51-100");
        sut.PageDetailText.Should().Be("of 140 messages");
    }
}
