using CommunityToolkit.Mvvm.ComponentModel;

namespace BusLane.ViewModels.Core;

public partial class PaginationState : ObservableObject
{
    [ObservableProperty]
    private int _currentPage = 1;
    
    [ObservableProperty]
    private bool _canGoNext;
    
    [ObservableProperty]
    private bool _canGoPrevious;
    
    [ObservableProperty]
    private string _pageInfoText = string.Empty;
    
    private int _messagesPerPage;
    private int _totalMessagesLoaded;
    
    public void UpdatePageInfo(int currentPage, int messagesPerPage, bool hasMoreMessages)
    {
        CurrentPage = currentPage;
        _messagesPerPage = messagesPerPage;
        
        var startMessage = (currentPage - 1) * messagesPerPage + 1;
        var endMessage = startMessage + messagesPerPage - 1;
        
        CanGoPrevious = currentPage > 1;
        CanGoNext = hasMoreMessages;
        
        PageInfoText = $"Page {currentPage} ({startMessage}-{endMessage})";
    }
    
    public void UpdatePageInfoWithTotal(int currentPage, int messagesPerPage, int totalMessages)
    {
        CurrentPage = currentPage;
        _messagesPerPage = messagesPerPage;
        _totalMessagesLoaded = totalMessages;
        
        var startMessage = (currentPage - 1) * messagesPerPage + 1;
        var endMessage = Math.Min(startMessage + messagesPerPage - 1, totalMessages);
        
        CanGoPrevious = currentPage > 1;
        CanGoNext = endMessage < totalMessages;
        
        PageInfoText = $"Page {currentPage} ({startMessage}-{endMessage} of {totalMessages})";
    }
    
    public void GoToNextPage()
    {
        if (CanGoNext)
        {
            CurrentPage++;
        }
    }
    
    public void GoToPreviousPage()
    {
        if (CanGoPrevious)
        {
            CurrentPage--;
        }
    }
    
    public void Reset()
    {
        CurrentPage = 1;
        CanGoNext = false;
        CanGoPrevious = false;
        PageInfoText = string.Empty;
        _totalMessagesLoaded = 0;
    }
}
