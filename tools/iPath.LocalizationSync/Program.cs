using iPath.Application.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

var repoRoot = Directory.GetCurrentDirectory();
bool write = false;
bool purge = false;

for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--repo-root":
            if (i + 1 >= args.Length)
            {
                Console.Error.WriteLine("--repo-root requires a path argument.");
                return 2;
            }
            repoRoot = Path.GetFullPath(args[++i]);
            break;
        case "--write":
            write = true;
            break;
        case "--purge":
            purge = true;
            break;
        case "sync":
            // the only command today - accepted as a no-op positional for a readable invocation
            break;
        case "-h":
        case "--help":
            PrintUsage();
            return 0;
        default:
            Console.Error.WriteLine($"Unknown argument: {args[i]}");
            PrintUsage();
            return 2;
    }
}

if (purge && !write)
{
    Console.WriteLine("Note: --purge has no effect without --write (this run only previews what would be removed).");
}

var uiSourceRoot = Path.Combine(repoRoot, "src", "ui");
var localesRoot = Path.Combine(repoRoot, "src", "ui", "iPath.Blazor.Server", "Locales");

if (!Directory.Exists(uiSourceRoot))
{
    Console.Error.WriteLine($"UI source root not found: {uiSourceRoot} (wrong --repo-root?)");
    return 2;
}

var supportedCultures = new[] { "en", "de", "fr", "it" };
var settings = Options.Create(new LocalizationSettings
{
    LocalesRoot = localesRoot,
    SupportedCultures = supportedCultures,
});
var fileService = new LocalizationFileService(settings, NullLogger<LocalizationFileService>.Instance);

var scan = LocalizationKeyScanner.ScanSourceForKeys(uiSourceRoot);

Console.WriteLine($"Scanned {scan.FilesScanned} files under {uiSourceRoot}, {scan.CallSitesScanned} T[...] call sites, {scan.Keys.Count} unique keys.");

if (scan.NonLiteralCallSites.Count > 0)
{
    Console.WriteLine();
    Console.WriteLine($"Non-literal call sites ({scan.NonLiteralCallSites.Count}) - not scanned, needs manual review:");
    foreach (var site in scan.NonLiteralCallSites)
    {
        Console.WriteLine($"  {site.File}:{site.Line}: {site.Snippet}");
    }
}

bool anyDrift = false;

foreach (var locale in supportedCultures)
{
    var data = fileService.GetTranslationData(locale);
    var (missing, orphaned) = LocalizationKeyScanner.Diff(scan.Keys, data);

    var present = scan.Keys.Count - missing.Count;
    Console.WriteLine();
    Console.WriteLine($"{locale}.json: {present}/{scan.Keys.Count} keys present, {missing.Count} missing, {orphaned.Count} orphaned");

    if (missing.Count > 0)
    {
        anyDrift = true;
        foreach (var key in missing)
        {
            Console.WriteLine($"  + missing: \"{key}\"");
        }
    }

    if (orphaned.Count > 0)
    {
        if (purge) anyDrift = true;
        foreach (var key in orphaned)
        {
            Console.WriteLine($"  - orphaned{(purge ? " (will be removed)" : "")}: \"{key}\"");
        }
    }

    if (write && (missing.Count > 0 || (purge && orphaned.Count > 0)))
    {
        LocalizationKeyScanner.ApplyChanges(data, missing, orphaned, purge);
        fileService.SaveTranslation(data);
        Console.WriteLine($"  saved {locale}.json");
    }
}

Console.WriteLine();
if (!write && anyDrift)
{
    Console.WriteLine("Dry run - no files changed. Re-run with --write to persist.");
}
else if (!anyDrift)
{
    Console.WriteLine("All locale files are in sync with source.");
}

return anyDrift ? 1 : 0;

static void PrintUsage()
{
    Console.WriteLine("""
        Usage: iPath.LocalizationSync sync [--repo-root <path>] [--write] [--purge]

          --repo-root <path>   Repository root (default: current directory)
          --write               Persist missing-key additions (and purge, if requested) to Locales/*.json
          --purge               Remove keys found in a locale file but not in source (requires --write to take effect)

        Without --write, this is a dry run: it only reports what would change and exits 1 if any
        locale has missing keys (or, with --purge, orphaned keys), 0 if everything is in sync.
        """);
}
