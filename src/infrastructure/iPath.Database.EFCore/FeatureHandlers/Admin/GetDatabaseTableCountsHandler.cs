using iPath.Application.Features.Admin;
using System.Data;

namespace iPath.EF.Core.FeatureHandlers.Admin;

public class GetDatabaseTableCountsHandler(iPathDbContext db, ILogger<GetDatabaseTableCountsHandler> logger)
    : IRequestHandler<GetDatabaseTableCountsQuery, Task<List<TableRowCountDto>>>
{
    public async Task<List<TableRowCountDto>> Handle(GetDatabaseTableCountsQuery request, CancellationToken ct)
    {
        var results = new List<TableRowCountDto>();

        foreach (var entityType in db.Model.GetEntityTypes())
        {
            var tableName = entityType.GetTableName();
            if (tableName is null || entityType.IsOwned())
                continue;

            try
            {
                var count = await GetRowCount(db, tableName);
                results.Add(new TableRowCountDto { TableName = tableName, RowCount = count });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error counting rows for table {Table}", tableName);
            }
        }

        return [.. results.OrderBy(r => r.TableName)];
    }

    private static async Task<long> GetRowCount(iPathDbContext db, string tableName)
    {
        var conn = db.Database.GetDbConnection();
        var wasOpen = conn.State == ConnectionState.Open;
        if (!wasOpen) await conn.OpenAsync();
        try
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"SELECT COUNT(*) FROM \"{tableName}\"";
            var result = await cmd.ExecuteScalarAsync();
            return result is long l ? l : Convert.ToInt64(result);
        }
        finally
        {
            if (!wasOpen) await conn.CloseAsync();
        }
    }
}
