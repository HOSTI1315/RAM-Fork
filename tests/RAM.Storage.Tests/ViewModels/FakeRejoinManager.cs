using RAM.Core.Abstractions;
using RAM.Core.Models;
using RAM.Roblox.Rejoin;

namespace RAM.Storage.Tests.ViewModels;

/// <summary>No-op rejoin manager for VM tests. Records calls so tests can assert handoff
/// behavior without spinning up real watchers / processes.</summary>
internal sealed class FakeRejoinManager : IRejoinManager
{
    public List<(Account Account, LaunchResult Result, LaunchTarget Target)> LaunchedCalls { get; } = new();
    public List<ulong> DisabledCalls { get; } = new();
    public List<ulong> RemovedCalls { get; } = new();
    public int ShutdownCalls { get; private set; }

    public void OnAccountLaunched(
        Account account,
        LaunchResult result,
        LaunchTarget target,
        Action<RejoinWorkerState>? workerStateChanged = null)
    {
        LaunchedCalls.Add((account, result, target));
    }

    public void OnAccountDisabled(ulong userId) => DisabledCalls.Add(userId);

    public Task OnAccountRemovedAsync(ulong userId)
    {
        RemovedCalls.Add(userId);
        return Task.CompletedTask;
    }

    public Task ShutdownAsync(CancellationToken ct = default)
    {
        ShutdownCalls++;
        return Task.CompletedTask;
    }
}
