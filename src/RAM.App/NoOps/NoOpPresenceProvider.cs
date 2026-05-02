using RAM.Core.Abstractions;
using RAM.Core.Models;

namespace RAM.App.NoOps;

internal sealed class NoOpPresenceProvider : IPresenceProvider
{
    private static readonly IReadOnlyDictionary<ulong, UserPresence> Empty
        = new Dictionary<ulong, UserPresence>();

    public Task<IReadOnlyDictionary<ulong, UserPresence>> GetPresenceAsync(
        IReadOnlyCollection<ulong> userIds, CancellationToken ct = default)
        => Task.FromResult(Empty);
}
