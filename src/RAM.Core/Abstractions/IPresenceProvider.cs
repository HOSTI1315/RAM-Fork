using RAM.Core.Models;

namespace RAM.Core.Abstractions;

public interface IPresenceProvider
{
    Task<IReadOnlyDictionary<ulong, UserPresence>> GetPresenceAsync(
        IReadOnlyCollection<ulong> userIds,
        CancellationToken ct = default);
}
