using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace RAM.Roblox.Launch;

/// <summary>
/// Multi-instance enabler #2 — the "773 fix": opens a <see cref="FileShare.Read"/> handle
/// on <c>RobloxCookies.dat</c> during the launch window so the Roblox launcher can't
/// CLOBBER (write) account-A's cookie when account-B starts. Reads from Roblox stay
/// allowed — that was the bug in v0.1.0–v0.1.2 with <see cref="FileShare.None"/>: a
/// running Roblox holds the file too, our acquire-loop never broke, and launch hung.
///
/// <para><b>Distinct from <see cref="SingletonMutexBypass"/></b> — that bypass enables
/// parallel processes; this lock prevents per-launch cookie file write races. The mutex
/// alone covers serial launches; the cookie lock is an extra safety net for true
/// parallel launches (which most users don't do).</para>
///
/// <para>If the lock can't be acquired within <see cref="AcquireTimeout"/>, we log a
/// warning and let the caller proceed without it. Better one-launch-without-lock than
/// zero launches — cookie clobber is rare and recoverable; a hung launcher is a brick.</para>
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class CookieFileLock : IDisposable
{
    public static readonly string DefaultCookieFilePath = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Roblox", "LocalStorage", "RobloxCookies.dat");

    private static readonly TimeSpan RetryInterval = TimeSpan.FromMilliseconds(50);

    /// <summary>Maximum total wait for <see cref="AcquireAsync"/> before giving up
    /// and returning a no-op releaser. 5s is plenty for a quick cookie-touch by another
    /// process; longer than that suggests the file is permanently held.</summary>
    public TimeSpan AcquireTimeout { get; init; } = TimeSpan.FromSeconds(5);

    private readonly string _path;
    private readonly SemaphoreSlim _inProcessGate = new(1, 1);
    private readonly ILogger<CookieFileLock> _logger;
    private FileStream? _handle;

    public CookieFileLock() : this(DefaultCookieFilePath, NullLogger<CookieFileLock>.Instance) { }

    public CookieFileLock(ILogger<CookieFileLock> logger) : this(DefaultCookieFilePath, logger) { }

    public CookieFileLock(string cookieFilePath, ILogger<CookieFileLock>? logger = null)
    {
        _path = cookieFilePath;
        _logger = logger ?? NullLogger<CookieFileLock>.Instance;
    }

    public bool IsLocked => _handle is not null;
    public string FilePath => _path;

    /// <summary>
    /// Acquires the lock. Returns an <see cref="IDisposable"/> that releases it on
    /// dispose. If acquisition takes longer than <see cref="AcquireTimeout"/>, returns
    /// a NO-OP releaser and logs a warning so the launch can proceed.
    /// </summary>
    public async Task<IDisposable> AcquireAsync(CancellationToken ct = default)
    {
        await _inProcessGate.WaitAsync(ct);
        try
        {
            EnsureDirectory();
            var deadline = DateTime.UtcNow + AcquireTimeout;
            var attempts = 0;
            while (!ct.IsCancellationRequested && DateTime.UtcNow < deadline)
            {
                try
                {
                    _handle = new FileStream(
                        _path,
                        FileMode.OpenOrCreate,
                        FileAccess.ReadWrite,
                        FileShare.Read,             // ← was None; allow Roblox to read its own file
                        bufferSize: 1,
                        FileOptions.None);
                    if (attempts > 0)
                        _logger.LogDebug("Cookie lock acquired after {Attempts} retries", attempts);
                    return new Releaser(this);
                }
                catch (IOException ex)
                {
                    attempts++;
                    if (attempts == 1 || attempts % 20 == 0) // ~1s
                        _logger.LogTrace(ex, "Cookie file busy (attempt {Attempt}); retrying", attempts);
                    await Task.Delay(RetryInterval, ct);
                }
                catch (UnauthorizedAccessException ex)
                {
                    _logger.LogWarning(ex,
                        "Cookie file access denied at {Path}; proceeding without lock", _path);
                    return NoOpReleaser.Instance.Also(_ => _inProcessGate.Release());
                }
            }
            ct.ThrowIfCancellationRequested();

            // Timeout reached. Don't fail the launch — give the caller a no-op releaser
            // and log a warning. The mutex bypass still protects against most clobber cases.
            _logger.LogWarning(
                "Could not acquire cookie file lock within {Timeout} after {Attempts} attempts " +
                "(file held by another process). Proceeding without lock — concurrent cookie " +
                "clobber protection is OFF for this launch.",
                AcquireTimeout, attempts);
            return NoOpReleaser.Instance.Also(_ => _inProcessGate.Release());
        }
        catch
        {
            _inProcessGate.Release();
            throw;
        }
    }

    /// <summary>
    /// Non-blocking attempt. Returns <c>true</c> only if the lock was newly acquired by
    /// this call. If the in-process gate or file is already held, returns <c>false</c>.
    /// </summary>
    public bool TryAcquireImmediate()
    {
        if (!_inProcessGate.Wait(0)) return false;
        try
        {
            EnsureDirectory();
            _handle = new FileStream(
                _path,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.Read,
                bufferSize: 1,
                FileOptions.None);
            return true;
        }
        catch (IOException)
        {
            _inProcessGate.Release();
            return false;
        }
    }

    private void EnsureDirectory()
    {
        var dir = System.IO.Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
    }

    private void ReleaseInternal()
    {
        _handle?.Dispose();
        _handle = null;
        _inProcessGate.Release();
    }

    public void Dispose()
    {
        _handle?.Dispose();
        _handle = null;
        _inProcessGate.Dispose();
    }

    private sealed class Releaser : IDisposable
    {
        private readonly CookieFileLock _owner;
        private bool _disposed;

        public Releaser(CookieFileLock owner) => _owner = owner;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _owner.ReleaseInternal();
        }
    }

    /// <summary>Returned when <see cref="AcquireAsync"/> times out — disposing it is
    /// a no-op so the caller's <c>using</c> block doesn't crash.</summary>
    private sealed class NoOpReleaser : IDisposable
    {
        public static readonly NoOpReleaser Instance = new();
        public void Dispose() { /* no-op */ }
    }
}

internal static class NoOpReleaserExtensions
{
    /// <summary>Run an action and return the same value — lets us release the in-process
    /// gate when returning a NoOp without a separate statement.</summary>
    public static T Also<T>(this T value, Action<T> action)
    {
        action(value);
        return value;
    }
}
