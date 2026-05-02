namespace RAM.SmokeTests;

internal static class Banner
{
    public static void Pass(string name, TimeSpan elapsed, string? detail)
    {
        WithColor(ConsoleColor.Green, () =>
            Console.WriteLine($"  PASS — {elapsed.TotalSeconds:F2}s"));
        if (!string.IsNullOrEmpty(detail))
            Console.WriteLine($"  └─ {detail}");
    }

    public static void Fail(string name, TimeSpan elapsed, Exception ex)
    {
        WithColor(ConsoleColor.Red, () =>
            Console.WriteLine($"  FAIL — {elapsed.TotalSeconds:F2}s"));
        Console.WriteLine($"  └─ {ex.GetType().Name}: {ex.Message}");
        if (ex.StackTrace is { } st)
            Console.WriteLine($"  └─ {st.Split('\n').FirstOrDefault()?.Trim()}");
    }

    private static void WithColor(ConsoleColor color, Action a)
    {
        var prev = Console.ForegroundColor;
        Console.ForegroundColor = color;
        try { a(); } finally { Console.ForegroundColor = prev; }
    }
}
