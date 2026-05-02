using Microsoft.Extensions.Logging.Abstractions;
using RAM.Core.Models;
using RAM.Storage;
using RAM.Storage.Ini;

namespace RAM.Storage.Tests;

public class SettingsStoreTests
{
    private static SettingsStore BuildStore(string root) =>
        new(new RamDataDirectory(root), NullLogger<SettingsStore>.Instance);

    [Fact]
    public async Task Defaults_loaded_when_file_missing()
    {
        using var tmp = new TempDir();
        var store = BuildStore(tmp.Path);
        var s = await store.LoadAsync();
        Assert.Equal(1, s.SchemaVersion);
        Assert.True(s.MultiInstanceEnabled);
        Assert.Equal(BotProfile.Normal, s.DefaultProfile);
        Assert.Equal(8, s.BackupRetentionHours);
    }

    [Fact]
    public async Task Save_then_load_roundtrips_changes()
    {
        using var tmp = new TempDir();
        var store = BuildStore(tmp.Path);

        var modified = new AppSettings
        {
            MultiInstanceEnabled = false,
            DefaultProfile = BotProfile.BottingBot,
            BackupRetentionHours = 24,
            RejoinCheckIntervalSeconds = 30,
            MemoryThresholdMb = 150,
            DefaultProxy = "http://proxy.local:8080",
            PresenceProvider = PresenceProviderKind.Polling,
        };
        await store.SaveAsync(modified);

        var loaded = await store.LoadAsync();
        Assert.False(loaded.MultiInstanceEnabled);
        Assert.Equal(BotProfile.BottingBot, loaded.DefaultProfile);
        Assert.Equal(24, loaded.BackupRetentionHours);
        Assert.Equal(30, loaded.RejoinCheckIntervalSeconds);
        Assert.Equal(150, loaded.MemoryThresholdMb);
        Assert.Equal("http://proxy.local:8080", loaded.DefaultProxy);
    }

    [Fact]
    public async Task Future_schema_version_throws_clean_error()
    {
        using var tmp = new TempDir();
        var path = Path.Combine(tmp.Path, "RAMSettings.ini");
        await File.WriteAllTextAsync(path, "[meta]\nschema_version=99\n");

        var store = BuildStore(tmp.Path);
        var ex = await Assert.ThrowsAsync<InvalidDataException>(() => store.LoadAsync());
        Assert.Contains("schema_version=99", ex.Message);
    }
}
