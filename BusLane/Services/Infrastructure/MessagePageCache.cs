namespace BusLane.Services.Infrastructure;

using BusLane.Models;

public class MessagePageCache
{
    private static readonly IReadOnlyList<MessageInfo> EmptyPage = [];
    private readonly Dictionary<int, IReadOnlyList<MessageInfo>> _pages = new();
    private readonly Dictionary<long, int> _sequenceNumberRefs = new();
    private int _totalCachedMessages;

    public void StorePage(int pageNumber, IEnumerable<MessageInfo> messages)
    {
        if (_pages.TryGetValue(pageNumber, out var existingMessages))
        {
            _totalCachedMessages -= existingMessages.Count;
            foreach (var message in existingMessages)
            {
                RemoveSequenceNumber(message.SequenceNumber);
            }
        }

        var pageMessages = messages.ToList().AsReadOnly();
        _pages[pageNumber] = pageMessages;
        _totalCachedMessages += pageMessages.Count;

        foreach (var message in pageMessages)
        {
            AddSequenceNumber(message.SequenceNumber);
        }
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
        return _sequenceNumberRefs.Count == 0 ? null : _sequenceNumberRefs.Keys.Max();
    }

    public void Clear()
    {
        _pages.Clear();
        _sequenceNumberRefs.Clear();
        _totalCachedMessages = 0;
    }

    public int GetTotalCachedMessages()
    {
        return _totalCachedMessages;
    }

    public HashSet<long> GetCachedSequenceNumbers()
    {
        return new HashSet<long>(_sequenceNumberRefs.Keys);
    }

    private void AddSequenceNumber(long sequenceNumber)
    {
        _sequenceNumberRefs.TryGetValue(sequenceNumber, out var count);
        _sequenceNumberRefs[sequenceNumber] = count + 1;
    }

    private void RemoveSequenceNumber(long sequenceNumber)
    {
        if (!_sequenceNumberRefs.TryGetValue(sequenceNumber, out var count))
        {
            return;
        }

        if (count <= 1)
        {
            _sequenceNumberRefs.Remove(sequenceNumber);
            return;
        }

        _sequenceNumberRefs[sequenceNumber] = count - 1;
    }
}
