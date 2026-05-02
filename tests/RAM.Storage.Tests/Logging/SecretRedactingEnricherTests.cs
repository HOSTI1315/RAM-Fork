using RAM.Storage.Logging;
using Serilog;
using Serilog.Sinks.InMemory;

namespace RAM.Storage.Tests.Logging;

public class SecretRedactingEnricherTests
{
    [Fact]
    public void Cookie_value_is_redacted_inline()
    {
        var input = "set-cookie .ROBLOSECURITY=_|WARNING:-DO-NOT-SHARE-THIS|_ABCDEF1234567890; path=/";
        var redacted = SecretRedactingEnricher.Redact(input);
        Assert.DoesNotContain("ABCDEF1234567890", redacted);
        Assert.DoesNotContain("DO-NOT-SHARE", redacted);
        Assert.Contains("***", redacted);
    }

    [Fact]
    public void Csrf_token_with_prefix_is_redacted()
    {
        var input = "X-CSRF-TOKEN: AAA-secret-token-zzz";
        var redacted = SecretRedactingEnricher.Redact(input);
        Assert.DoesNotContain("secret-token-zzz", redacted);
        Assert.Contains("X-CSRF-TOKEN", redacted, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("***", redacted);
    }

    [Fact]
    public void Auth_ticket_with_prefix_is_redacted()
    {
        var input = "rbx-authentication-ticket: longticket12345";
        var redacted = SecretRedactingEnricher.Redact(input);
        Assert.DoesNotContain("longticket12345", redacted);
        Assert.Contains("***", redacted);
    }

    [Fact]
    public void Bearer_token_is_redacted()
    {
        var input = "Authorization: Bearer eyJhbGciOiJIUzI1NiJ9.payload.sig";
        var redacted = SecretRedactingEnricher.Redact(input);
        Assert.DoesNotContain("eyJhbGc", redacted);
        Assert.Contains("***", redacted);
    }

    [Fact]
    public void Plain_text_is_unchanged()
    {
        var input = "user 12345 launched roblox";
        Assert.Equal(input, SecretRedactingEnricher.Redact(input));
    }

    [Fact]
    public void Sensitive_property_names_are_redacted_wholesale()
    {
        var sink = new InMemorySink();
        var logger = new LoggerConfiguration()
            .Enrich.With(new SecretRedactingEnricher())
            .WriteTo.Sink(sink)
            .CreateLogger();

        logger.Information("login {Cookie}", "_|WARNING:-DO-NOT-SHARE-THIS|_DEEPSECRET");
        logger.Information("csrf refreshed {CsrfToken}", "AAA-csrf-bbb");
        logger.Information("ticket {AuthTicket}", "GHIJKL-MNOP-very-secret");
        logger.Information("bearer {BearerToken}", "eyJSECRETJWT.aaa.bbb");
        logger.Information("user passed {Password}", "hunter2");
        logger.Information("authorization header {Authorization}", "Basic dXNlcjpwYXNz");

        var allText = string.Join("\n", sink.LogEvents.Select(RenderEvent));

        Assert.DoesNotContain("DEEPSECRET", allText);
        Assert.DoesNotContain("AAA-csrf-bbb", allText);
        Assert.DoesNotContain("GHIJKL-MNOP-very-secret", allText);
        Assert.DoesNotContain("eyJSECRETJWT", allText);
        Assert.DoesNotContain("hunter2", allText);
        Assert.DoesNotContain("dXNlcjpwYXNz", allText);
    }

    [Fact]
    public void Embedded_patterns_in_property_values_are_redacted()
    {
        var sink = new InMemorySink();
        var logger = new LoggerConfiguration()
            .Enrich.With(new SecretRedactingEnricher())
            .WriteTo.Sink(sink)
            .CreateLogger();

        // RawHeader is not a "sensitive" name, but its value contains an embedded pattern.
        logger.Information("got header {RawHeader}", "X-CSRF-TOKEN: PATTERN-MATCH-secret-xyz");
        var text = string.Join("\n", sink.LogEvents.Select(RenderEvent));
        Assert.DoesNotContain("PATTERN-MATCH-secret-xyz", text);
        Assert.Contains("***", text);
    }

    [Fact]
    public void Non_sensitive_property_passes_through()
    {
        var sink = new InMemorySink();
        var logger = new LoggerConfiguration()
            .Enrich.With(new SecretRedactingEnricher())
            .WriteTo.Sink(sink)
            .CreateLogger();

        logger.Information("user {Username} logged in", "alice");
        var text = string.Join("\n", sink.LogEvents.Select(RenderEvent));
        Assert.Contains("alice", text);
    }

    private static string RenderEvent(Serilog.Events.LogEvent e)
    {
        var props = string.Join(",", e.Properties.Select(p => $"{p.Key}={p.Value}"));
        return $"{e.RenderMessage()} | {props}";
    }
}
