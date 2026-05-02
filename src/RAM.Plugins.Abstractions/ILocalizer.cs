namespace RAM.Plugins.Abstractions;

public interface ILocalizer
{
    string this[string key] { get; }
    string Format(string key, params object[] args);
    string CurrentCulture { get; }
}
