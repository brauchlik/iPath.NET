using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;

namespace iPath.API.Sqlite;

public class SqliteWalInterceptor : DbConnectionInterceptor
{
    public override Task ConnectionOpenedAsync(DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        if (connection is SqliteConnection sqlite)
        {
            var cmd = sqlite.CreateCommand();
            cmd.CommandText = "PRAGMA journal_mode=WAL;";
            return cmd.ExecuteNonQueryAsync(cancellationToken);
        }
        return Task.CompletedTask;
    }
}