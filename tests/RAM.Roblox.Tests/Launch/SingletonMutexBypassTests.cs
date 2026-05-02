using RAM.Roblox.Launch;

namespace RAM.Roblox.Tests.Launch;

public class SingletonMutexBypassTests
{
    private static string TestName() => $"RAM_TEST_MUTEX_{Guid.NewGuid():N}";

    [Fact]
    public void Acquire_then_release_round_trips_state()
    {
        using var bypass = new SingletonMutexBypass(TestName());
        bypass.Acquire();
        Assert.True(bypass.IsActive);
        Assert.True(bypass.HasOwnership);
        bypass.Release();
        Assert.False(bypass.IsActive);
    }

    [Fact]
    public void Second_thread_cannot_steal_ownership_while_first_holds()
    {
        // Mutex semantics are thread-affined. Use a real dedicated Thread (not Task.Run,
        // which can occasionally inline on the calling thread under xunit's parallel
        // scheduler and yield false ownership re-entry).
        var name = TestName();

        using var first = new SingletonMutexBypass(name);
        first.Acquire();
        Assert.True(first.HasOwnership);

        var box = new bool[1];
        var thread = new Thread(() =>
        {
            using var second = new SingletonMutexBypass(name);
            second.Acquire();
            box[0] = second.HasOwnership;
        }) { IsBackground = true };
        thread.Start();
        thread.Join();

        Assert.False(box[0]);
    }

    [Fact]
    public void Mutex_name_constant_matches_roblox_singleton()
    {
        Assert.Equal("ROBLOX_singletonMutex", SingletonMutexBypass.MutexName);
    }
}
