using iPath.Application.Contracts;
using iPath.Google.Email;
using iPath.Google.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace iPath.Google;

public static class GoogleServicesRegistration
{
    public static IServiceCollection AddGoogleServices(this IServiceCollection services, IConfiguration config)
    {
        var cfg = new GmailConfig();
        config.GetSection(nameof(GmailConfig)).Bind(cfg);

        if (cfg.Active)
        {
            services.Configure<GmailConfig>(config.GetSection(nameof(GmailConfig)));
            services.AddScoped<IMailBox, GmailIMapReader>();
        }

        return services;
    }


    public static bool AddGoogleDriveServices(this IServiceCollection services, IConfiguration config)
    {
        var cfg = new GoogleDriveConfig();
        config.GetSection(nameof(GoogleDriveConfig)).Bind(cfg);

        if (cfg.Active)
        {
            services.Configure<GoogleDriveConfig>(config.GetSection(nameof(GoogleDriveConfig)));
            services.AddScoped<IRemoteStorageService, GoogleDriveStorageService>();
            services.AddHttpClient("GoogleDrive");
            // services.AddCors(options => options.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

            return true;
        }

        return false;
    }
}