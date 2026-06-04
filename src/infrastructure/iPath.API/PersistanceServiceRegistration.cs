using iPath.API.Services;
using iPath.Application.Features.Notifications;
using iPath.EF.Core.Database;
using iPath.EF.Core.FeatureHandlers.Emails;
using iPath.EF.Core.FeatureHandlers.Groups;
using iPath.EF.Core.FeatureHandlers.Notifications;
using iPath.Google;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;


namespace iPath.API;

public static class PersistanceServiceRegistration
{
    public static IServiceCollection AddPersistance(this IServiceCollection services, IConfiguration config)
    {
        var provider = config.GetSection("DbProvider").Value ?? DBProvider.Sqlite.Name; // read from appsettings and default to sqlite
        Console.WriteLine("DbProvider = " + provider);

        services.AddDbContext<iPathDbContext>(cfg =>
        {
            if (provider == DBProvider.InMemory.Name)
            {
                cfg.UseInMemoryDatabase("ipath");
            }
            else if (provider == DBProvider.Postgres.Name)
            {
                cfg.UseNpgsql(
                    config.GetConnectionString(DBProvider.Postgres.Name),
                    x => x.MigrationsAssembly(DBProvider.Postgres.Assembly)
                );
            }
            /*
            if (provider == DBProvider.SqlServer.Name)
            {
                var cs = config.GetConnectionString(DBProvider.SqlServer.Name);
                cfg.UseSqlServer(
                    config.GetConnectionString(DBProvider.SqlServer.Name),
                    x => x.MigrationsAssembly(DBProvider.SqlServer.Assembly)
                );
            }
            */
            else if (provider == DBProvider.Sqlite.Name)
            {
                var cs = config.GetConnectionString(DBProvider.Sqlite.Name);
                cfg.UseSqlite(
                    config.GetConnectionString(DBProvider.Sqlite.Name) ?? "ipath.db", // default to ipath.db
                    x => x.MigrationsAssembly(DBProvider.Sqlite.Assembly)
                );
            }
            else
            {
                throw new Exception("no db provider configuration found");
            }

            cfg.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        });

        // services.AddDbFactory(config);

        services.AddScoped<DbSeeder>();
        services.AddScoped<IEmailRepository, EmailRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<IGroupService, GroupService>();
        services.AddScoped<IGroupCache, GroupCacheServer>();

        // Google Workspace
        services.AddGoogleServices(config);


        return services;
    }

    private static IServiceCollection AddDbFactory(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContextFactory<iPathDbContext>(cfg =>
        {
            var provider = config.GetSection("DbProvider").Value ?? DBProvider.Postgres.Name;
            // Console.WriteLine(provider);

            if (provider == DBProvider.InMemory.Name)
            {
                cfg.UseInMemoryDatabase("ipath");
            }
            else if (provider == DBProvider.Postgres.Name)
            {
                cfg.UseNpgsql(
                    config.GetConnectionString(DBProvider.Postgres.Name),
                    x => x.MigrationsAssembly(DBProvider.Postgres.Assembly)
                );
            }
            /*
            if (provider == DBProvider.SqlServer.Name)
            {
                var cs = config.GetConnectionString(DBProvider.SqlServer.Name);
                cfg.UseSqlServer(
                    config.GetConnectionString(DBProvider.SqlServer.Name),
                    x => x.MigrationsAssembly(DBProvider.SqlServer.Assembly)
                );
            }
            */
            else if (provider == DBProvider.Sqlite.Name)
            {
                cfg.UseSqlite(
                    config.GetConnectionString(DBProvider.Sqlite.Name),
                    x => x.MigrationsAssembly(DBProvider.Sqlite.Assembly)
                );
            }
            else
            {
                throw new Exception("no db provider configuration found");
            }

            cfg.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        });

        return services;
    }

    public static async Task UpdateDatabase(this IHost host)
    {
        using var scope = host.Services.CreateScope();
        var seeder = scope.ServiceProvider.GetRequiredService<DbSeeder>();

        await seeder.UpdateDatabase();
    }
}


public record DBProvider(string Name, string Assembly)
{
    public static DBProvider InMemory = new(nameof(InMemory), null); // InMemory DB has no migrations
    public static DBProvider Sqlite = new(nameof(Sqlite), typeof(iPath.EF.Sqlite.Marker).Assembly.GetName().Name!);
    public static DBProvider Postgres = new(nameof(Postgres), typeof(iPath.EF.Postgres.Marker).Assembly.GetName().Name!);
    // public static DBProvider SqlServer = new(nameof(SqlServer), typeof(iPath.EF.SqlServer.Marker).Assembly.GetName().Name!);
}