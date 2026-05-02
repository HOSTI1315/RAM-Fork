namespace RAM.Plugins.Abstractions;

public interface IThemeSource
{
    string ActiveThemeName { get; }
    IReadOnlyDictionary<string, string> Tokens { get; }
    event EventHandler? ThemeChanged;
}
