using RAM.Roblox.Launch;

namespace RAM.Roblox.Tests.Launch;

public class CookieFileLockTests
{
    private static string TempPath() =>
        Path.Combine(Path.GetTempPath(), $"ram-cookielock-{Guid.NewGuid():N}.dat");

    [Fact]
    public async Task AcquireAsync_creates_file_and_returns_releaser()
    {
        var path = TempPath();
        try
        {
            using var fileLock = new CookieFileLock(path);
            using (await fileLock.AcquireAsync())
            {
                Assert.True(fileLock.IsLocked);
                Assert.True(File.Exists(path));
            }
            Assert.False(fileLock.IsLocked);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public async Task Concurrent_acquires_within_one_instance_serialize()
    {
        var path = TempPath();
        try
        {
            using var fileLock = new CookieFileLock(path);
            int inside = 0, maxConcurrent = 0;
            var tasks = Enumerable.Range(0, 5).Select(_ => Task.Run(async () =>
            {
                using (await fileLock.AcquireAsync())
                {
                    var c = Interlocked.Increment(ref inside);
                    InterlockedMax(ref maxConcurrent, c);
                    await Task.Delay(50);
                    Interlocked.Decrement(ref inside);
                }
            })).ToArray();
            await Task.WhenAll(tasks);

            Assert.Equal(1, maxConcurrent);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public async Task Cross_instance_acquires_serialize_via_file_share_none()
    {
        var path = TempPath();
        try
        {
            using var first = new CookieFileLock(path);
            using var second = new CookieFileLock(path);

            using var firstHandle = await first.AcquireAsync();

            // Second instance can't acquire while first holds; it should be cancelled.
            using var ctsBlock = new CancellationTokenSource(TimeSpan.FromMilliseconds(150));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => second.AcquireAsync(ctsBlock.Token));

            firstHandle.Dispose();

            using var ctsOk = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            using var secondHandle = await second.AcquireAsync(ctsOk.Token);
            Assert.True(second.IsLocked);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public async Task TryAcquireImmediate_returns_false_when_other_instance_holds_file()
    {
        var path = TempPath();
        try
        {
            using var first = new CookieFileLock(path);
            using var second = new CookieFileLock(path);

            using var firstHandle = await first.AcquireAsync();
            Assert.False(second.TryAcquireImmediate());
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public void Default_path_points_to_roblox_local_storage()
    {
        Assert.EndsWith(@"Roblox\LocalStorage\RobloxCookies.dat", CookieFileLock.DefaultCookieFilePath);
    }

    private static void InterlockedMax(ref int location, int value)
    {
        int initial;
        do { initial = location; if (value <= initial) return; }
        while (Interlocked.CompareExchange(ref location, value, initial) != initial);
    }
}
