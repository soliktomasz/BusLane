namespace BusLane.Services.ServiceBus;

using BusLane.Models;

public interface ICorrelationMessageCatalog
{
    void Add(CorrelationMessage message);
    void AddRange(IEnumerable<CorrelationMessage> messages);
    IReadOnlyList<CorrelationGroup> GetGroups();
    void Clear();
}

public sealed class CorrelationMessageCatalog : ICorrelationMessageCatalog
{
    private readonly int _capacity;
    private readonly object _lock = new();
    private readonly LinkedList<CorrelationMessage> _messages = [];
    private readonly Dictionary<MessageIdentity, LinkedListNode<CorrelationMessage>> _nodes = [];

    public CorrelationMessageCatalog(int capacity = 2_000)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }

        _capacity = capacity;
    }

    public void Add(CorrelationMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        lock (_lock)
        {
            var identity = MessageIdentity.From(message);
            if (_nodes.Remove(identity, out var existing))
            {
                _messages.Remove(existing);
            }

            var node = _messages.AddLast(message);
            _nodes[identity] = node;

            while (_messages.Count > _capacity)
            {
                var oldest = _messages.First!;
                _messages.RemoveFirst();
                _nodes.Remove(MessageIdentity.From(oldest.Value));
            }
        }
    }

    public void AddRange(IEnumerable<CorrelationMessage> messages)
    {
        ArgumentNullException.ThrowIfNull(messages);
        foreach (var message in messages)
        {
            Add(message);
        }
    }

    public IReadOnlyList<CorrelationGroup> GetGroups()
    {
        CorrelationMessage[] snapshot;
        lock (_lock)
        {
            snapshot = _messages.ToArray();
        }

        return snapshot
            .Select(static message => (Message: message, Group: GetGroupIdentity(message)))
            .Where(static item => item.Group.HasValue)
            .GroupBy(static item => item.Group!.Value.Key, StringComparer.Ordinal)
            .Select(static group =>
            {
                var first = group.First().Group!.Value;
                var messages = group
                    .Select(static item => item.Message)
                    .OrderBy(static message => message.EnqueuedTime)
                    .ThenBy(static message => message.SequenceNumber)
                    .ToList();
                return new CorrelationGroup(first.Key, first.DisplayId, first.UsesSessionFallback, messages);
            })
            .OrderByDescending(static group => group.Messages[^1].EnqueuedTime)
            .ToList();
    }

    public void Clear()
    {
        lock (_lock)
        {
            _messages.Clear();
            _nodes.Clear();
        }
    }

    private static GroupIdentity? GetGroupIdentity(CorrelationMessage message)
    {
        if (!string.IsNullOrWhiteSpace(message.CorrelationId))
        {
            return new GroupIdentity($"corr:{message.CorrelationId}", message.CorrelationId, false);
        }

        if (!string.IsNullOrWhiteSpace(message.SessionId))
        {
            return new GroupIdentity($"session:{message.SessionId}", message.SessionId, true);
        }

        return null;
    }

    private readonly record struct GroupIdentity(string Key, string DisplayId, bool UsesSessionFallback);

    private readonly record struct MessageIdentity(
        string NamespaceName,
        string EntityName,
        long SequenceNumber,
        string MessageId)
    {
        public static MessageIdentity From(CorrelationMessage message)
        {
            return new MessageIdentity(
                message.NamespaceName,
                message.EntityName,
                message.SequenceNumber,
                message.MessageId);
        }
    }
}
