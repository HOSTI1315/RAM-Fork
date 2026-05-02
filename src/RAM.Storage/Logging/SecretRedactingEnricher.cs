using System.Text.RegularExpressions;
using Serilog.Core;
using Serilog.Events;

namespace RAM.Storage.Logging;

/// <summary>
/// Serilog enricher that scrubs known Roblox secret patterns from log property values
/// and replaces values of sensitively-named properties with <c>***</c>.
///
/// Two layers of defense:
/// <list type="number">
///   <item>Property name match: any property whose name contains "cookie", "csrf", "token",
///         "ticket", "bearer", "password", "secret", or equals "authorization" gets its value
///         replaced wholesale with <c>***</c>.</item>
///   <item>Value pattern match: regex over property values catches embedded patterns
///         (<c>.ROBLOSECURITY=...</c>, <c>X-CSRF-TOKEN: ...</c>, raw cookie warnings).</item>
/// </list>
///
/// Limitation: Serilog message templates are immutable, so secrets embedded directly into
/// the message template text (e.g. <c>logger.Information("token: AAA-csrf-bbb")</c>) cannot
/// be redacted. Always parameterize secrets via <c>{PropertyName}</c> placeholders.
/// </summary>
public sealed partial class SecretRedactingEnricher : ILogEventEnricher
{
    private const string Replacement = "***";

    private static readonly string[] SensitivePropertyHints =
    [
        "cookie", "csrf", "token", "ticket", "bearer", "password", "secret",
    ];

    [GeneratedRegex(@"\.ROBLOSECURITY=[^;\s""]+", RegexOptions.IgnoreCase)]
    private static partial Regex CookiePattern();

    [GeneratedRegex(@"_\|WARNING:-DO-NOT-SHARE-THIS\|_[A-Za-z0-9+/=_-]+", RegexOptions.IgnoreCase)]
    private static partial Regex CookieValuePattern();

    [GeneratedRegex(@"(?i)(x-csrf-token\s*[:=]\s*)[A-Za-z0-9+/=_-]+")]
    private static partial Regex CsrfPattern();

    [GeneratedRegex(@"(?i)(rbx-authentication-ticket\s*[:=]\s*)[A-Za-z0-9+/=_-]+")]
    private static partial Regex AuthTicketPattern();

    [GeneratedRegex(@"(?i)(authorization\s*:\s*bearer\s+)[A-Za-z0-9._-]+")]
    private static partial Regex BearerPattern();

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var updates = new List<LogEventProperty>();
        foreach (var (name, value) in logEvent.Properties)
        {
            if (IsSensitiveName(name))
            {
                updates.Add(new LogEventProperty(name, new ScalarValue(Replacement)));
                continue;
            }
            var redacted = RedactValue(value);
            if (!ReferenceEquals(redacted, value))
                updates.Add(new LogEventProperty(name, redacted));
        }
        foreach (var p in updates)
            logEvent.AddOrUpdateProperty(p);
    }

    private static bool IsSensitiveName(string propertyName)
    {
        if (propertyName.Equals("Authorization", StringComparison.OrdinalIgnoreCase))
            return true;
        foreach (var hint in SensitivePropertyHints)
            if (propertyName.Contains(hint, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    public static string Redact(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        var s = CookieValuePattern().Replace(input, Replacement);
        s = CookiePattern().Replace(s, $".ROBLOSECURITY={Replacement}");
        s = CsrfPattern().Replace(s, $"$1{Replacement}");
        s = AuthTicketPattern().Replace(s, $"$1{Replacement}");
        s = BearerPattern().Replace(s, $"$1{Replacement}");
        return s;
    }

    private static LogEventPropertyValue RedactValue(LogEventPropertyValue value) => value switch
    {
        ScalarValue { Value: string s } => new ScalarValue(Redact(s)),
        SequenceValue seq => new SequenceValue(seq.Elements.Select(RedactValue)),
        StructureValue str => new StructureValue(
            str.Properties.Select(p => new LogEventProperty(p.Name, RedactValue(p.Value))),
            str.TypeTag),
        DictionaryValue dict => new DictionaryValue(
            dict.Elements.Select(kv => new KeyValuePair<ScalarValue, LogEventPropertyValue>(
                kv.Key, RedactValue(kv.Value)))),
        _ => value,
    };
}
