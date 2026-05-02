using RAM.Plugins.Abstractions;

namespace RAM.App.NoOps;

internal sealed class NoOpThemeSource : IThemeSource
{
    public string ActiveThemeName => "default";
    public IReadOnlyDictionary<string, string> Tokens { get; } = new Dictionary<string, string>();

    public event EventHandler? ThemeChanged
    {
        add { }
        remove { }
    }
}
