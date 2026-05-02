using RAM.SmokeTests;
using RAM.SmokeTests.Scenarios;

var results = new List<(string Name, bool Passed, string? Detail, TimeSpan Elapsed)>();

async Task Run(string name, Func<Task<string?>> scenario)
{
    Console.WriteLine();
    Console.WriteLine($"=== {name} ===");
    var sw = System.Diagnostics.Stopwatch.StartNew();
    try
    {
        var detail = await scenario();
        sw.Stop();
        results.Add((name, true, detail, sw.Elapsed));
        Banner.Pass(name, sw.Elapsed, detail);
    }
    catch (Exception ex)
    {
        sw.Stop();
        results.Add((name, false, ex.Message, sw.Elapsed));
        Banner.Fail(name, sw.Elapsed, ex);
    }
}

await Run("Test 1 — DPAPI legacy migration",     MigrationSmokeTest.RunAsync);
await Run("Test 2 — Multi-launch serialization", MultiLaunchSmokeTest.RunAsync);
await Run("Test 3 — FlogWatcher disconnect",     FlogWatcherSmokeTest.RunAsync);
await Run("Test 4 — DI host VM resolution",      DiHostSmokeTest.RunAsync);
await Run("Test 5 — Schema v1→v2 migration",     SchemaV2MigrationSmokeTest.RunAsync);
await Run("Test 6 — Rejoin FSM end-to-end",       RejoinFsmSmokeTest.RunAsync);

Console.WriteLine();
Console.WriteLine("=== Summary ===");
foreach (var r in results)
{
    var tag = r.Passed ? "PASS" : "FAIL";
    Console.WriteLine($"  [{tag}] {r.Name} — {r.Elapsed.TotalSeconds:F2}s");
    if (!string.IsNullOrEmpty(r.Detail))
        Console.WriteLine($"         {r.Detail}");
}

var failed = results.Count(r => !r.Passed);
Environment.ExitCode = failed == 0 ? 0 : 1;
Console.WriteLine();
Console.WriteLine($"{results.Count - failed}/{results.Count} scenarios passed.");
