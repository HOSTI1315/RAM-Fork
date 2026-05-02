using System.Globalization;
using RAM.Plugins.Abstractions;

namespace RAM.App.NoOps;

internal sealed class NoOpLocalizer : ILocalizer
{
    public string this[string key] => key;
    public string Format(string key, params object[] args)
        => string.Format(CultureInfo.InvariantCulture, key, args);
    public string CurrentCulture => "en";
}
