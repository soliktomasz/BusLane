using BusLane.Models;

namespace BusLane.Services.Infrastructure;

public class MessagePageCache
{
    private static readonly IReadOnlyList<MessageInfo> EmptyPage = [];
    private readonly Dictionary<int, IReadOnlyList<MessageInfo>> _pages = new();

    public void StorePage(int pageNumber, IEnumerable<MessageInfo> messages)
    {
        _pages[pageNumber] = messages.ToList().AsReadOnly();
    }

    public IReadOnlyList<MessageInfo> GetPage(int pageNumber)
    {
        return _pages.TryGetValue(pageNumber, out var messages)
            ? messages
            : EmptyPage;
    }

    public bool HasPage(int pageNumber)
    {
        return _pages.ContainsKey(pageNumber);
    }

    public long? GetLastSequenceNumber(int pageNumber)
    {
        if (!_pages.TryGetValue(pageNumber, out var messages) || messages.Count == 0) return null;

        // Return the MAXIMUM sequence number, not the last element in the sorted list.
        // Messages are sorted by EnqueuedTime for display, but pagination needs
        // the highest sequence number to fetch the next batch of messages.
        var maxSequenceNumber = messages[0].SequenceNumber;
        for (var i = 1; i < messages.Count; i++)
        {
            if (messages[i].SequenceNumber > maxSequenceNumber)
            {
                maxSequenceNumber = messages[i].SequenceNumber;
            }
        }

        return maxSequenceNumber;
    }

    public long? GetMaxSequenceNumber()
    {
        if (_pages.Count == 0) return null;

        long? maxSequenceNumber = null;

        foreach (var page in _pages.Values)
        {
            foreach (var message in page)
            {
                if (!maxSequenceNumber.HasValue || message.SequenceNumber > maxSequenceNumber.Value)
                {
                    maxSequenceNumber = message.SequenceNumber;
                }
            }
        }

        return maxSequenceNumber;
    }

    public void Clear()
    {
        _pages.Clear();
    }

    public int GetTotalCachedMessages()
    {
        var total = 0;
        foreach (var page in _pages.Values)
        {
            total += page.Count;
        }

        return total;
    }

    public HashSet<long> GetCachedSequenceNumbers()
    {
        var sequenceNumbers = new HashSet<long>();

        foreach (var page in _pages.Values)
        {
            foreach (var message in page)
            {
                sequenceNumbers.Add(message.SequenceNumber);
            }
        }

        return sequenceNumbers;
    }
}
