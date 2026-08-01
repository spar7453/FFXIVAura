using System.Reflection;

var tests = new List<(string Name, Action Run)>();

// Collect every static *Tests class in the assembly regardless of namespace, so a suite
// added under a folder-derived namespace (e.g. FFXIVAura.Tests.Auras) fails loudly via the
// Cases contract below instead of being silently skipped by a namespace filter.
var testSuiteTypes = Assembly.GetExecutingAssembly()
    .GetTypes()
    .Where(type => type.IsAbstract && type.IsSealed)
    .Where(type => type.Name.EndsWith("Tests", StringComparison.Ordinal))
    .OrderBy(type => type.FullName, StringComparer.Ordinal);
foreach (var suiteType in testSuiteTypes)
{
    var casesProperty = suiteType.GetProperty("Cases", BindingFlags.Public | BindingFlags.Static);
    if (casesProperty?.GetValue(null) is not IEnumerable<(string Name, Action Run)> cases)
        throw new InvalidOperationException($"{suiteType.FullName} must expose public static Cases.");

    tests.AddRange(cases);
}

var failed = 0;
foreach (var (name, run) in tests)
{
    try
    {
        run();
        Console.WriteLine($"PASS {name}");
    }
    catch (Exception ex)
    {
        failed++;
        Console.WriteLine($"FAIL {name}: {ex.Message}");
    }
}

if (failed > 0)
{
    Console.WriteLine($"{failed} test(s) failed.");
    return failed;
}

Console.WriteLine($"{tests.Count} tests passed.");
return 0;
