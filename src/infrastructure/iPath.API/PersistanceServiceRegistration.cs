using iPath.Application.Features.Notifications;
using iPath.EF.Core.Database;
using iPath.EF.Core.FeatureHandlers.Emails;
using iPath.EF.Core.FeatureHandlers.Groups;
using iPath.EF.Core.FeatureHandlers.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;


namespace iPath.API;

public static class PersistanceServiceRegistration
{
    public static IServiceCollection AddPersistance(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<iPathDbContext>(cfg =>
        {
            var provider = config.GetSection("DbProvider").Value ?? DBProvider.Postgres.Name;
            // Console.WriteLine(provider);

            if (provider == DBProvider.Postgres.Name)
            {
                throw new NotImplementedException();
                //cfg.UseNpgsql(
                //    config.GetConnectionString(DBProvider.Postgres.Name),
                //    x => x.MigrationsAssembly(Postgres.Assembly)
                //);
            }
            if (provider == DBProvider.Sqlite.Name)
            {
                var cs = config.GetConnectionString(DBProvider.Sqlite.Name);
                cfg.UseSqlite(
                    config.GetConnectionString(DBProvider.Sqlite.Name),
                    x => x.MigrationsAssembly(DBProvider.Sqlite.Assembly)
                );
            }
        });

        // services.AddDbFactory(config);

        services.AddScoped<DbSeeder>();
        services.AddScoped<IEmailRepository, EmailRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<IGroupService, GroupService>();

        return services;
    }

    private static IServiceCollection AddDbFactory(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContextFactory<iPathDbContext>(cfg =>
        {
            var provider = config.GetSection("DbProvider").Value ?? DBProvider.Postgres.Name;
            // Console.WriteLine(provider);

            if (provider == DBProvider.Postgres.Name)
            {
                throw new NotImplementedException();
                //cfg.UseNpgsql(
                //    config.GetConnectionString(DBProvider.Postgres.Name),
                //    x => x.MigrationsAssembly(DBProvider.Postgres.Assembly)
                //)
                // .UseSnakeCaseNamingConvention();
            }
            if (provider == DBProvider.Sqlite.Name)
            {
                cfg.UseSqlite(
                    config.GetConnectionString(DBProvider.Sqlite.Name),
                    x => x.MigrationsAssembly(DBProvider.Sqlite.Assembly)
                )
                .UseSnakeCaseNamingConvention();
            }
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
    public static DBProvider Sqlite = new(nameof(Sqlite), typeof(iPath.EF.Sqlite.Marker).Assembly.GetName().Name!);
    public static DBProvider Postgres = new(nameof(Postgres), typeof(iPath.EF.Postgres.Marker).Assembly.GetName().Name!);
}