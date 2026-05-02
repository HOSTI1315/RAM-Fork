using RAM.App.ViewModels;
using RAM.Core.Abstractions;
using RAM.Core.Models;

namespace RAM.Storage.Tests.ViewModels;

public class SettingsViewModelTests
{
    private sealed class FakeStore : ISettingsStore
    {
        public AppSettings Saved { get; private set; } = new();
        public int SaveCount { get; private set; }
        public Task<AppSettings> LoadAsync(CancellationToken ct = default) => Task.FromResult(Saved);
        public Task SaveAsync(AppSettings settings, CancellationToken ct = default)
        {
            Saved = settings; SaveCount++; return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Load_populates_observable_properties()
    {
        var store = new FakeStore();
        await store.SaveAsync(new AppSettings
        {
            MultiInstanceEnabled = false,
            DefaultProfile = BotProfile.BottingBot,
            BackupRetentionHours = 24,
            DefaultProxy = "http://proxy.local:8080",
        });

        var vm = new SettingsViewModel(store);
        await vm.LoadAsync();

        Assert.False(vm.MultiInstanceEnabled);
        Assert.Equal(BotProfile.BottingBot, vm.DefaultProfile);
        Assert.Equal(24, vm.BackupRetentionHours);
        Assert.Equal("http://proxy.local:8080", vm.DefaultProxy);
        Assert.False(vm.IsDirty);
    }

    [Fact]
    public async Task Editing_marks_dirty_and_save_persists()
    {
        var store = new FakeStore();
        var vm = new SettingsViewModel(store);
        await vm.LoadAsync();

        vm.MemoryThresholdMb = 150;
        Assert.True(vm.IsDirty);

        await vm.SaveAsync();

        Assert.False(vm.IsDirty);
        Assert.Equal(1, store.SaveCount);
        Assert.Equal(150, store.Saved.MemoryThresholdMb);
    }

    [Fact]
    public async Task Cancel_reverts_pending_edits()
    {
        var store = new FakeStore();
        await store.SaveAsync(new AppSettings { MemoryThresholdMb = 200 });
        var vm = new SettingsViewModel(store);
        await vm.LoadAsync();

        vm.MemoryThresholdMb = 999;
        vm.Cancel();

        Assert.Equal(200, vm.MemoryThresholdMb);
        Assert.False(vm.IsDirty);
    }

    [Fact]
    public void Available_profiles_lists_all_three_modes()
    {
        var vm = new SettingsViewModel(new FakeStore());
        Assert.Equal(3, vm.AvailableProfiles.Count);
        Assert.Contains(BotProfile.Normal, vm.AvailableProfiles);
        Assert.Contains(BotProfile.BottingPlayer, vm.AvailableProfiles);
        Assert.Contains(BotProfile.BottingBot, vm.AvailableProfiles);
    }

    [Fact]
    public void Empty_proxy_string_normalizes_to_null_in_settings()
    {
        var vm = new SettingsViewModel(new FakeStore());
        vm.DefaultProxy = "";
        var s = vm.ToSettings();
        Assert.Null(s.DefaultProxy);
    }
}
