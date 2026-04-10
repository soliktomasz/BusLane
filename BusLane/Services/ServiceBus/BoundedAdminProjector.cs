namespace BusLane.Services.ServiceBus;

using System.Collections.Generic;

/// <summary>
/// Projects admin API work with a bounded level of concurrency while preserving source order.
/// </summary>
internal static class BoundedAdminProjector
{
    internal const int DefaultMaxConcurrency = 8;

    public static async Task<IReadOnlyList<TResult>> SelectAsync<TSource, TResult>(
        IReadOnlyList<TSource> source,
        Func<TSource, CancellationToken, Task<TResult>> projector,
        int maxConcurrency = DefaultMaxConcurrency,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(projector);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxConcurrency, 1);

        if (source.Count == 0)
        {
            return [];
        }

        var results = new TResult[source.Count];
        using var gate = new SemaphoreSlim(Math.Min(maxConcurrency, source.Count));

        var tasks = source
            .Select((item, index) => ProjectAsync(item, index, projector, results, gate, ct))
            .ToArray();

        await Task.WhenAll(tasks);
        return results;
    }

    private static async Task ProjectAsync<TSource, TResult>(
        TSource item,
        int index,
        Func<TSource, CancellationToken, Task<TResult>> projector,
        TResult[] results,
        SemaphoreSlim gate,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        await gate.WaitAsync(ct);

        try
        {
            results[index] = await projector(item, ct);
        }
        finally
        {
            gate.Release();
        }
    }
}
