using System.Runtime.Versioning;

namespace RAM.Roblox.Launch;

/// <summary>
/// Multi-instance enabler #2 — the "773 fix": opens an exclusive
/// <see cref="FileShare.None"/> handle on the Roblox cookies file during the launch
/// window so the Roblox launcher can't clobber account-A's cookie when account-B starts.
///
/// <para><b>Distinct from <see cref="SingletonMutexBypass"/></b> — that bypass enables
/// parallel processes; this lock prevents per-launch cookie file races. Both required.</para>
///
/// <para>Serialization is two-layer:
/// <list type="bullet">
///   <item><b>In-process</b>: a <see cref="SemaphoreSlim"/> serializes concurrent
///         <see cref="AcquireAsync"/> calls within the same singleton instance.</item>
///   <item><b>Cross-instance / cross-process</b>: the underlying file is opened with
///         <see cref="FileShare.None"/>; competing openers retry until they succeed
///         (or hit the cancellation token).</item>
/// </list>
/// </para>
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class CookieFileLock : IDisposable
{
    public static readonly string DefaultCookieFilePath = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Roblox", "LocalStorage", "RobloxCookies.dat");

    private static readonly TimeSpan RetryInterval = TimeSpan.FromMilliseconds(50);

    private readonly string _path;
    private readonly SemaphoreSlim _inProcessGate = new(1, 1);
    private FileStream? _handle;

    public CookieFileLock() : this(DefaultCookieFilePath) { }

    public CookieFileLock(string cookieFilePath) => _path = cookieFilePath;

    public bool IsLocked => _handle is not null;
    public string FilePath => _path;

    /// <summary>
    /// Acquires the lock. Returns an <see cref="IDisposable"/> that releases it on
    /// dispose. Awaits until the lock is available or <paramref name="ct"/> cancels.
    /// </summary>
    public async Task<IDisposable> AcquireAsync(CancellationToken ct = default)
    {
        await _inProcessGate.WaitAsync(ct);
        try
        {
            EnsureDirectory();
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    _handle = new FileStream(
                        _path,
                        FileMode.OpenOrCreate,
                        FileAccess.ReadWrite,
                        FileShare.None,
                        bufferSize: 1,
                        FileOptions.None);
                    return new Releaser(this);
                }
                catch (IOException)
                {
                    await Task.Delay(RetryInterval, ct);
                }
            }
            ct.ThrowIfCancellationRequested();
            throw new OperationCanceledException(ct);
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
                FileShare.None,
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
}
