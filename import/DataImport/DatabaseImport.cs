using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace iPath.DataImport;

public static class DatabaseImport
{
    public static async Task StartImport(IServiceProvider hostProvider)
    {
        using IServiceScope serviceScope = hostProvider.CreateScope();
        IServiceProvider provider = serviceScope.ServiceProvider;
        ImportService srv = provider.GetRequiredService<ImportService>();

        IOptions<ImportConfig> opts = provider.GetRequiredService<IOptions<ImportConfig>>();
        var cfg = opts.Value;
        if (cfg.BulkSize == 0)
        {
            Console.WriteLine("importsettings.json is not configured propertly");
            return;
        }


        Console.WriteLine("Database Migrations ...");
        await srv.MigrateDatbase();

        /*
            Console.WriteLine("Insert Check ...");
            await srv.TestInsert();
            return;
        */




        srv.BulkSize = cfg.BulkSize;

        /**/
        Console.WriteLine("creating username chat array ...");
        var unamechars = await srv.GetUserNameChars();
        

        Console.WriteLine("loading new id chache ...");
        await srv.InitAsync();

        if (cfg.ImportUsers)
        {
            Console.WriteLine("importing users ... ");
            await srv.ImportUsersAsync();
        }

        if (cfg.ImportCommunities)
        {
            Console.WriteLine("importing communities ... ");
            await srv.ImportCommunitiesAsync();
        }

        if (cfg.ImportGroups)
        {
            Console.WriteLine("importing groups ... ");
            await srv.ImportGroupsAsync();
        }

        if (cfg.ImportServiceRequests)
        {
            Console.WriteLine("importing ServiceRequests ... ");
            await srv.ImportRequestsAsync(true);
        }

        if (cfg.ImportDocuments)
        {
            Console.WriteLine("importing Documents ... ");
            await srv.ÏmportDocumentsAsync(false);
        }

        if (cfg.ImportVisitStats)
        {
            Console.WriteLine("importing visit statitics ... ");
            await srv.ImportUserStatsAsync();
        }

        Console.WriteLine();
        Console.WriteLine("done");
    }

}