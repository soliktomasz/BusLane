namespace BusLane.ViewModels.Core;

using CommunityToolkit.Mvvm.ComponentModel;

public partial class PaginationState : ObservableObject
{
    [ObservableProperty]
    private int _currentPage = 1;
    
    [ObservableProperty]
    private bool _canGoNext;
    
    [ObservableProperty]
    private bool _canGoPrevious;
    
    [ObservableProperty]
    private bool _hasPageInfo;

    [ObservableProperty]
    private string _pageLabel = string.Empty;

    [ObservableProperty]
    private string _pageRangeText = string.Empty;

    [ObservableProperty]
    private string? _pageDetailText;
    
    public void UpdatePageInfo(int currentPage, int messagesPerPage, int actualMessageCount, bool hasMoreMessages)
    {
        CurrentPage = currentPage;

        CanGoPrevious = currentPage > 1;
        CanGoNext = hasMoreMessages;

        if (actualMessageCount <= 0)
        {
            ClearPageInfo();
            return;
        }

        var startMessage = (currentPage - 1L) * messagesPerPage + 1;
        var endMessage = startMessage + actualMessageCount - 1L;

        SetPageInfo(
            $"Page {currentPage}",
            $"Showing {startMessage}-{endMessage}");
    }
    
    public void UpdatePageInfoWithTotal(int currentPage, int messagesPerPage, long totalMessages)
    {
        CurrentPage = currentPage;

        CanGoPrevious = currentPage > 1;

        if (totalMessages <= 0)
        {
            CanGoNext = false;
            ClearPageInfo();
            return;
        }

        var startMessage = (currentPage - 1L) * messagesPerPage + 1;
        var endMessage = Math.Min(startMessage + messagesPerPage - 1L, totalMessages);

        CanGoNext = endMessage < totalMessages;

        SetPageInfo(
            $"Page {currentPage}",
            $"Showing {startMessage}-{endMessage}",
            $"of {totalMessages} messages");
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
        ClearPageInfo();
    }

    private void SetPageInfo(string pageLabel, string pageRangeText, string? pageDetailText = null)
    {
        HasPageInfo = true;
        PageLabel = pageLabel;
        PageRangeText = pageRangeText;
        PageDetailText = pageDetailText;
    }

    private void ClearPageInfo()
    {
        HasPageInfo = false;
        PageLabel = string.Empty;
        PageRangeText = string.Empty;
        PageDetailText = null;
    }
}
