using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;

namespace RAM.Roblox.Launch;

/// <summary>
/// Locates the current Roblox player install directory. Uses
/// <c>%LocalAppData%\Roblox\Versions\&lt;version-guid&gt;</c> by picking the most recently
/// modified version folder containing <c>RobloxPlayerBeta.exe</c>.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class RobloxInstallLocator
{
    public static readonly string DefaultVersionsRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Roblox", "Versions");

    private readonly string _versionsRoot;
    private readonly ILogger<RobloxInstallLocator> _logger;

    public RobloxInstallLocator(ILogger<RobloxInstallLocator> logger)
        : this(DefaultVersionsRoot, logger) { }

    public RobloxInstallLocator(string versionsRoot, ILogger<RobloxInstallLocator> logger)
    {
        _versionsRoot = versionsRoot;
        _logger = logger;
    }

    public string? FindCurrentVersion()
    {
        if (!Directory.Exists(_versionsRoot)) return null;
        try
        {
            var dir = Directory.EnumerateDirectories(_versionsRoot)
                .Where(d => File.Exists(Path.Combine(d, "RobloxPlayerBeta.exe")))
                .OrderByDescending(d => Directory.GetLastWriteTimeUtc(d))
                .FirstOrDefault();
            return dir;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not enumerate Roblox versions root");
            return null;
        }
    }
}
