using iPath.API;
using iPath.API.Services.SyncImport;
using iPath.Application.Features.SyncImport;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var (appSettingsPath, groupIds) = ParseArgs(args);
if (string.IsNullOrEmpty(appSettingsPath) || groupIds.Count == 0)
{
    Console.Error.WriteLine("Usage: iPath.SyncTool --appsettings <path> --groups <csv>");
    return 1;
}

if (!File.Exists(appSettingsPath))
{
    Console.Error.WriteLine($"Appsettings file not found: {appSettingsPath}");
    return 1;
}

var host = Host.CreateApplicationBuilder(args);

var config = new ConfigurationBuilder()
    .SetBasePath(Path.GetDirectoryName(appSettingsPath)!)
    .AddJsonFile(appSettingsPath, optional: false)
    .Build();

host.Configuration.AddConfiguration(config);

host.Services.AddPersistance(host.Configuration);
host.Services.AddIPathAuthentication(host.Configuration);

var syncCs = config.GetConnectionString("ipath_old");
if (string.IsNullOrEmpty(syncCs))
{
    Console.Error.WriteLine("ConnectionStrings:ipath_old not found in appsettings");
    return 1;
}

host.Services.AddSingleton(new OldDataService(syncCs));
host.Services.AddScoped<ISyncImportRunner, SyncImportRunner>();

var app = host.Build();

using var scope = app.Services.CreateScope();
var runner = scope.ServiceProvider.GetRequiredService<ISyncImportRunner>();
var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

foreach (var gid in groupIds)
{
    Console.WriteLine($"Syncing group {gid}...");
    try
    {
        var progress = new Progress<(int Current, int Total, string Status)>(p =>
        {
            var pct = p.Total > 0 ? (int)(p.Current * 100.0 / p.Total) : 0;
            Console.Write($"\r  [{new string('#', pct / 5).PadRight(20)}] {pct}% — {p.Status}");
        });
        await runner.SyncGroupWithProgressAsync(gid, progress);
        Console.WriteLine();
        Console.WriteLine($"Group {gid} synced successfully.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to sync group {GroupId}", gid);
        Console.Error.WriteLine($"  ERROR: {ex.Message}");
    }
}

return 0;

static (string? appSettingsPath, List<int> groupIds) ParseArgs(string[] args)
{
    string? appSettingsPath = null;
    List<int> groupIds = [];

    for (int i = 0; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "--appsettings" when i + 1 < args.Length:
                appSettingsPath = args[++i];
                break;
            case "--groups" when i + 1 < args.Length:
                groupIds = args[++i]
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(s => int.TryParse(s, out var id) ? id : -1)
                    .Where(id => id > 0)
                    .ToList();
                break;
        }
    }

    return (appSettingsPath, groupIds);
}
