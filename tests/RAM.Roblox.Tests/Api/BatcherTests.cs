using RAM.Roblox.Api.Batching;

namespace RAM.Roblox.Tests.Api;

public class BatcherTests
{
    [Fact]
    public async Task Multiple_requests_within_window_collapse_into_single_executor_call()
    {
        var execCount = 0;
        IReadOnlyList<int>? receivedKeys = null;

        using var batcher = new Batcher<int, string>(
            window: TimeSpan.FromMilliseconds(40),
            maxBatchSize: 100,
            executor: (keys, ct) =>
            {
                Interlocked.Increment(ref execCount);
                receivedKeys = keys;
                return Task.FromResult<IReadOnlyDictionary<int, string>>(
                    keys.ToDictionary(k => k, k => $"v{k}"));
            });

        var t1 = batcher.RequestAsync(1);
        var t2 = batcher.RequestAsync(2);
        var t3 = batcher.RequestAsync(3);

        var r1 = await t1;
        var r2 = await t2;
        var r3 = await t3;

        Assert.Equal("v1", r1);
        Assert.Equal("v2", r2);
        Assert.Equal("v3", r3);
        Assert.Equal(1, execCount);
        Assert.NotNull(receivedKeys);
        Assert.Equal(3, receivedKeys!.Count);
    }

    [Fact]
    public async Task Batch_flushes_immediately_when_max_size_reached()
    {
        var execCount = 0;
        var firstBatchSize = 0;

        using var batcher = new Batcher<int, string>(
            window: TimeSpan.FromSeconds(5),
            maxBatchSize: 3,
            executor: (keys, ct) =>
            {
                Interlocked.Increment(ref execCount);
                if (firstBatchSize == 0) firstBatchSize = keys.Count;
                return Task.FromResult<IReadOnlyDictionary<int, string>>(
                    keys.ToDictionary(k => k, k => $"v{k}"));
            });

        var t1 = batcher.RequestAsync(1);
        var t2 = batcher.RequestAsync(2);
        var t3 = batcher.RequestAsync(3);

        await Task.WhenAll(t1, t2, t3);
        Assert.Equal(1, execCount);
        Assert.Equal(3, firstBatchSize);
    }

    [Fact]
    public async Task Same_key_requested_twice_returns_same_task()
    {
        using var batcher = new Batcher<int, string>(
            window: TimeSpan.FromMilliseconds(40),
            maxBatchSize: 100,
            executor: (keys, ct) => Task.FromResult<IReadOnlyDictionary<int, string>>(
                keys.ToDictionary(k => k, k => "v")));

        var a = batcher.RequestAsync(7);
        var b = batcher.RequestAsync(7);
        Assert.Same(a, b);
        Assert.Equal("v", await a);
    }

    [Fact]
    public async Task Executor_failure_propagates_to_all_pending_tasks()
    {
        using var batcher = new Batcher<int, string>(
            window: TimeSpan.FromMilliseconds(40),
            maxBatchSize: 100,
            executor: (keys, ct) => throw new InvalidOperationException("upstream failed"));

        var t1 = batcher.RequestAsync(1);
        var t2 = batcher.RequestAsync(2);

        await Assert.ThrowsAsync<InvalidOperationException>(() => t1);
        await Assert.ThrowsAsync<InvalidOperationException>(() => t2);
    }

    [Fact]
    public async Task FlushNow_dispatches_immediately()
    {
        var execCount = 0;
        using var batcher = new Batcher<int, string>(
            window: TimeSpan.FromSeconds(5),
            maxBatchSize: 100,
            executor: (keys, ct) =>
            {
                Interlocked.Increment(ref execCount);
                return Task.FromResult<IReadOnlyDictionary<int, string>>(
                    keys.ToDictionary(k => k, k => "v"));
            });

        var t = batcher.RequestAsync(1);
        batcher.FlushNow();
        await t;
        Assert.Equal(1, execCount);
    }
}
