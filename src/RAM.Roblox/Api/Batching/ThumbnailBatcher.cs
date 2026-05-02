using Microsoft.Extensions.Options;
using RAM.Core.Abstractions;

namespace RAM.Roblox.Api.Batching;

public sealed class ThumbnailBatcher : IDisposable
{
    private readonly Batcher<ThumbnailKey, ThumbnailResult> _batcher;

    public ThumbnailBatcher(IRobloxApi api, IOptions<RobloxApiOptions> options)
    {
        _batcher = new Batcher<ThumbnailKey, ThumbnailResult>(
            options.Value.ThumbnailBatchWindow,
            options.Value.ThumbnailBatchMaxSize,
            (keys, ct) => api.GetThumbnailsAsync(keys, ct));
    }

    public Task<ThumbnailResult?> RequestAsync(ThumbnailKey key, CancellationToken ct = default)
        => _batcher.RequestAsync(key, ct);

    public void Dispose() => _batcher.Dispose();
}
