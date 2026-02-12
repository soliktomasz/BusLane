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
            return messages[^1].SequenceNumber;
        }
        return null;
    }
    
    public void Clear()
    {
        _pages.Clear();
    }
    
    public int GetTotalCachedMessages()
    {
        return _pages.Values.Sum(p => p.Count);
    }
}
