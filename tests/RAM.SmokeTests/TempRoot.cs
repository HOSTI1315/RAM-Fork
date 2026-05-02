namespace RAM.SmokeTests;

internal sealed class TempRoot : IDisposable
{
    public string Path { get; }
    public TempRoot(string label)
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            $"ram-smoke-{label}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path);
    }
    public void Dispose()
    {
        try { Directory.Delete(Path, recursive: true); } catch { /* ignore */ }
    }
}
