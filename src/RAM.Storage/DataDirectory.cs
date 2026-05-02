using RAM.Core.Abstractions;

namespace RAM.Storage;

public sealed class RamDataDirectory : IDataDirectoryProvider
{
    public string DataDirectory { get; }
    public string BackupsDirectory { get; }

    public RamDataDirectory(string root)
    {
        DataDirectory = root;
        BackupsDirectory = Path.Combine(root, "backups");
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(BackupsDirectory);
    }

    public static RamDataDirectory Default()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return new RamDataDirectory(Path.Combine(localAppData, "RAM"));
    }
}
