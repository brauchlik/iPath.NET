using iPath.EF.Core.Database;
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
                //)
                // .UseSnakeCaseNamingConvention();
            }
            if (provider == DBProvider.Sqlite.Name)
            {
                var cs = config.GetConnectionString(DBProvider.Sqlite.Name);
                cfg.UseSqlite(
                    config.GetConnectionString(DBProvider.Sqlite.Name),
                    x => x.MigrationsAssembly(DBProvider.Sqlite.Assembly)
                )
                .UseSnakeCaseNamingConvention();
            }
        }, ServiceLifetime.Scoped);

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
        }, ServiceLifetime.Scoped);

        services.AddTransient<DbSeeder>();

        return services;
    }



    public static IHost UpdateDatabase(this IHost host)
    {
        using var scope = host.Services.CreateScope();
        var seeder = scope.ServiceProvider.GetRequiredService<DbSeeder>();

        seeder.UpdateDatabase();
        // seeder.Seed();

        return host;
    }
}


public record DBProvider(string Name, string Assembly)
{
    public static DBProvider Sqlite = new(nameof(Sqlite), typeof(iPath.EF.Sqlite.Marker).Assembly.GetName().Name!);
    public static DBProvider Postgres = new(nameof(Postgres), typeof(iPath.EF.Postgres.Marker).Assembly.GetName().Name!);
}