using System.Collections.Generic;
using System.Linq;
using BusLane.Models;

namespace BusLane.Services.Infrastructure;

public class MessagePageCache
{
    private readonly Dictionary<int, IReadOnlyList<MessageInfo>> _pages = new();

    public void StorePage(int pageNumber, IEnumerable<MessageInfo> messages)
    {
        _pages[pageNumber] = messages.ToList().AsReadOnly();
    }

    public IReadOnlyList<MessageInfo> GetPage(int pageNumber)
    {
        return _pages.TryGetValue(pageNumber, out var messages)
            ? messages
            : new List<MessageInfo>().AsReadOnly();
    }

    public bool HasPage(int pageNumber)
    {
        return _pages.ContainsKey(pageNumber);
    }

    public long? GetLastSequenceNumber(int pageNumber)
    {
        if (_pages.TryGetValue(pageNumber, out var messages) && messages.Count > 0)
        {
            // Return the MAXIMUM sequence number, not the last element in the sorted list.
            // Messages are sorted by EnqueuedTime for display, but pagination needs
            // the highest sequence number to fetch the next batch of messages.
            return messages.Max(m => m.SequenceNumber);
        }
        return null;
    }

    public long? GetMaxSequenceNumber()
    {
        if (_pages.Count == 0) return null;

        var allMessages = _pages.Values.SelectMany(p => p).ToList();
        return allMessages.Count > 0 ? allMessages.Max(m => m.SequenceNumber) : null;
    }

    public void Clear()
    {
        _pages.Clear();
    }

    public int GetTotalCachedMessages()
    {
        return _pages.Values.Sum(p => p.Count);
    }

    public HashSet<long> GetCachedSequenceNumbers()
    {
        return _pages.Values
            .SelectMany(p => p)
            .Select(m => m.SequenceNumber)
            .ToHashSet();
    }
}
