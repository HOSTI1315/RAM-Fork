using Microsoft.Extensions.Options;
using RAM.Core.Abstractions;
using RAM.Core.Models;

namespace RAM.Roblox.Api.Batching;

/// <summary>
/// Batches per-user presence requests within a 50ms window then dispatches one HTTP call.
/// Per cookie: a separate batcher because presence requests carry the caller's cookie.
/// </summary>
public sealed class PresenceBatcher : IDisposable
{
    private readonly IRobloxApi _api;
    private readonly RobloxApiOptions _options;
    private readonly Dictionary<string, Batcher<ulong, UserPresence>> _byCookie = new();
    private readonly object _gate = new();

    public PresenceBatcher(IRobloxApi api, IOptions<RobloxApiOptions> options)
    {
        _api = api;
        _options = options.Value;
    }

    public Task<UserPresence?> RequestAsync(ulong userId, string cookie, CancellationToken ct = default)
    {
        Batcher<ulong, UserPresence> batcher;
        lock (_gate)
        {
            if (!_byCookie.TryGetValue(cookie, out batcher!))
            {
                batcher = new Batcher<ulong, UserPresence>(
                    _options.PresenceBatchWindow,
                    _options.PresenceBatchMaxSize,
                    (ids, c) => _api.GetPresenceAsync(ids, cookie, c));
                _byCookie[cookie] = batcher;
            }
        }
        return batcher.RequestAsync(userId, ct);
    }

    public void Dispose()
    {
        lock (_gate)
        {
            foreach (var b in _byCookie.Values) b.Dispose();
            _byCookie.Clear();
        }
    }
}
