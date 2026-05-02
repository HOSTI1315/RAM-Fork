using RAM.Core.Models;

namespace RAM.Core.Abstractions;

public interface IAccountStore
{
    Task<IReadOnlyList<Account>> LoadAllAsync(CancellationToken ct = default);
    Task SaveAllAsync(IReadOnlyList<Account> accounts, CancellationToken ct = default);
}

public interface ISettingsStore
{
    Task<AppSettings> LoadAsync(CancellationToken ct = default);
    Task SaveAsync(AppSettings settings, CancellationToken ct = default);
}

public interface IDataDirectoryProvider
{
    string DataDirectory { get; }
    string BackupsDirectory { get; }
}
