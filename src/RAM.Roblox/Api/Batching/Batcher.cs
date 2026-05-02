namespace RAM.Roblox.Api.Batching;

/// <summary>
/// Generic timed batcher: collects requests within a sliding window, then dispatches one
/// batched call per group key. Each request returns its own result via TaskCompletionSource.
/// </summary>
public sealed class Batcher<TKey, TResult> : IDisposable
    where TKey : notnull
{
    private readonly TimeSpan _window;
    private readonly int _maxBatchSize;
    private readonly Func<IReadOnlyList<TKey>, CancellationToken, Task<IReadOnlyDictionary<TKey, TResult>>> _executor;
    private readonly object _gate = new();
    private readonly Dictionary<TKey, TaskCompletionSource<TResult?>> _pending = new();
    private CancellationTokenSource? _flushCts;
    private bool _disposed;

    public Batcher(
        TimeSpan window,
        int maxBatchSize,
        Func<IReadOnlyList<TKey>, CancellationToken, Task<IReadOnlyDictionary<TKey, TResult>>> executor)
    {
        _window = window;
        _maxBatchSize = maxBatchSize;
        _executor = executor;
    }

    public Task<TResult?> RequestAsync(TKey key, CancellationToken ct = default)
    {
        TaskCompletionSource<TResult?> tcs;
        bool flushNow = false;
        lock (_gate)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(Batcher<TKey, TResult>));
            if (_pending.TryGetValue(key, out var existing))
                return existing.Task;
            tcs = new TaskCompletionSource<TResult?>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pending[key] = tcs;
            if (_pending.Count >= _maxBatchSize) flushNow = true;
            else ScheduleFlushLocked();
        }
        ct.Register(() => tcs.TrySetCanceled(ct));
        if (flushNow) FlushNow();
        return tcs.Task;
    }

    private void ScheduleFlushLocked()
    {
        if (_flushCts != null) return;
        var cts = new CancellationTokenSource();
        _flushCts = cts;
        _ = Task.Run(async () =>
        {
            try { await Task.Delay(_window, cts.Token); }
            catch (OperationCanceledException) { return; }
            FlushNow();
        });
    }

    public void FlushNow()
    {
        Dictionary<TKey, TaskCompletionSource<TResult?>> snapshot;
        lock (_gate)
        {
            if (_pending.Count == 0) return;
            snapshot = new Dictionary<TKey, TaskCompletionSource<TResult?>>(_pending);
            _pending.Clear();
            _flushCts?.Cancel();
            _flushCts?.Dispose();
            _flushCts = null;
        }
        _ = ExecuteAsync(snapshot);
    }

    private async Task ExecuteAsync(Dictionary<TKey, TaskCompletionSource<TResult?>> batch)
    {
        try
        {
            var keys = batch.Keys.ToArray();
            var results = await _executor(keys, default);
            foreach (var (key, tcs) in batch)
            {
                if (results.TryGetValue(key, out var value))
                    tcs.TrySetResult(value);
                else
                    tcs.TrySetResult(default);
            }
        }
        catch (Exception ex)
        {
            foreach (var tcs in batch.Values) tcs.TrySetException(ex);
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            _flushCts?.Cancel();
            _flushCts?.Dispose();
            _flushCts = null;
            foreach (var tcs in _pending.Values)
                tcs.TrySetCanceled();
            _pending.Clear();
        }
    }
}
