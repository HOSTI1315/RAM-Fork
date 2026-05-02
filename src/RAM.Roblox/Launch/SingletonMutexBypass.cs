using System.Runtime.Versioning;

namespace RAM.Roblox.Launch;

/// <summary>
/// Multi-instance enabler #1: holds the named mutex <c>ROBLOX_singletonMutex</c> that
/// the Roblox launcher uses to prevent multiple instances. While we hold it, additional
/// Roblox processes can launch in parallel.
///
/// <para><b>Distinct from <see cref="CookieFileLock"/></b> — that one prevents per-launch
/// cookie races; this one bypasses single-instance enforcement. Both are required for
/// reliable parallel multi-launch.</para>
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class SingletonMutexBypass : IDisposable
{
    public const string MutexName = "ROBLOX_singletonMutex";

    private readonly string _name;
    private Mutex? _mutex;
    private bool _owned;

    public SingletonMutexBypass() : this(MutexName) { }

    public SingletonMutexBypass(string mutexName) => _name = mutexName;

    public bool IsActive => _mutex is not null;
    public bool HasOwnership => _owned;

    public void Acquire()
    {
        if (_mutex is not null) return;
        _mutex = new Mutex(initiallyOwned: false, _name, out _);
        try
        {
            _owned = _mutex.WaitOne(TimeSpan.Zero);
        }
        catch (AbandonedMutexException)
        {
            _owned = true;
        }
    }

    public void Release()
    {
        if (_mutex is null) return;
        if (_owned)
        {
            try { _mutex.ReleaseMutex(); } catch { /* ignore */ }
            _owned = false;
        }
        _mutex.Dispose();
        _mutex = null;
    }

    public void Dispose() => Release();
}
