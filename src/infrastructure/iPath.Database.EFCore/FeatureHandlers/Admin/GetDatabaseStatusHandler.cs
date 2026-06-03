using iPath.Application.Features.Admin;
using iPath.Domain.Config;
using Microsoft.Extensions.Options;
using System.Data;

namespace iPath.EF.Core.FeatureHandlers.Admin;

public class GetDatabaseStatusHandler(iPathDbContext db, IOptions<iPathConfig> opts, ILogger<GetDatabaseStatusHandler> logger)
    : IRequestHandler<GetDatabaseStatusQuery, Task<DatabaseStatusDto>>
{
    public async Task<DatabaseStatusDto> Handle(GetDatabaseStatusQuery request, CancellationToken ct)
    {
        var cfg = opts.Value;
        var connStr = db.Database.GetConnectionString() ?? "";
        connStr = MaskConnectionString(connStr);

        var dto = new DatabaseStatusDto
        {
            ProviderName = db.Database.ProviderName ?? "unknown",
            ConnectionString = connStr,
            AutoMigrate = cfg.DbAutoMigrate,
        };

        try
        {
            var applied = await db.Database.GetAppliedMigrationsAsync(ct);
            dto.AppliedMigrations = applied.Select(ParseMigration).ToList();
            if (applied.Any())
                dto.LastMigration = applied.Last();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error loading applied migrations");
        }

        try
        {
            dto.PendingMigrations = (await db.Database.GetPendingMigrationsAsync(ct))
                .Select(ParseMigration).ToList();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error loading pending migrations");
        }

        try
        {
            dto.DbFileSize = await GetDbFileSize(db);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting database file size");
        }

        return dto;
    }

    private static MigrationItemDto ParseMigration(string migrationId)
    {
        var created = "";
        if (migrationId.Length >= 14 && long.TryParse(migrationId[..14], out _))
        {
            created = $"{migrationId[..4]}-{migrationId.Substring(4, 2)}-{migrationId.Substring(6, 2)} {migrationId.Substring(8, 2)}:{migrationId.Substring(10, 2)}:{migrationId.Substring(12, 2)}";
        }
        var name = migrationId.Length > 15 ? migrationId[15..] : migrationId;
        return new MigrationItemDto { Name = name, Created = created };
    }

    private static string MaskConnectionString(string cs)
    {
        var parts = cs.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var kept = parts.Where(p =>
        {
            var key = p.Split('=')[0].Trim().ToLowerInvariant();
            return key is "data source" or "datasource" or "host" or "server" or "database" or "initial catalog";
        });
        return string.Join("; ", kept);
    }

    private static async Task<string> GetDbFileSize(iPathDbContext db)
    {
        var provider = db.Database.ProviderName;
        var conn = db.Database.GetDbConnection();

        if (provider?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true)
        {
            await conn.OpenAsync();
            try
            {
                await using var cmd = conn.CreateCommand();

                cmd.CommandText = "PRAGMA page_count";
                var pageCount = (long)(await cmd.ExecuteScalarAsync())!;

                cmd.CommandText = "PRAGMA page_size";
                var pageSize = (long)(await cmd.ExecuteScalarAsync())!;

                var bytes = pageCount * pageSize;
                return FormatFileSize(bytes);
            }
            finally
            {
                await conn.CloseAsync();
            }
        }

        if (provider?.Contains("Postgres", StringComparison.OrdinalIgnoreCase) == true)
        {
            await conn.OpenAsync();
            try
            {
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT pg_database_size(current_database())";
                var bytes = (long)(await cmd.ExecuteScalarAsync())!;
                return FormatFileSize(bytes);
            }
            finally
            {
                await conn.CloseAsync();
            }
        }

        return "";
    }

    private static string FormatFileSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:N1} KB",
        < 1024 * 1024 * 1024 => $"{bytes / (1024.0 * 1024):N1} MB",
        _ => $"{bytes / (1024.0 * 1024 * 1024):N2} GB"
    };
}
