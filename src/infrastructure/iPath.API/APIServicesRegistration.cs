using DispatchR.Extensions;
using iPath.API.Services;
using iPath.API.Services.Email;
using iPath.API.Services.Storage;
using iPath.API.Services.Thumbnail;
using iPath.API.Services.Uploads;
using iPath.Application.Contracts;
using iPath.Application.Localization;
using iPath.EF.Core.FeatureHandlers.Users.Queries;
using iPath.RazorLib.Localization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json.Serialization;

namespace iPath.API;

public static class APIServicesRegistration
{
    public static IServiceCollection AddIPathAPI(this IServiceCollection services, IConfiguration config)
    {
        // configure Mediator
        services.AddDispatchR(cfg =>
        {
            cfg.Assemblies.Add(typeof(iPath.API.Meta).Assembly);
            cfg.Assemblies.Add(typeof(iPath.EF.Core.Meta).Assembly);
        });

        // EF Core and Authentication
        services.AddPersistance(config);
        services.AddIPathAuthentication(config);

        // Email handling
        services.Configure<SmtpConfig>(options => config.GetSection(nameof(SmtpConfig)).Bind(options));
        services.AddTransient<IEmailSender, SmtpEmailSender>();
        services.AddSingleton<IEmailQueue, EmailQueue>(ctx =>
        {
            var capacity = config.GetValue<int?>("Norifications:ProcessingQueueCapacity") ?? 100;
            return new EmailQueue(capacity);
        });
        services.AddHostedService<EmailQueueWorker>();

        // Upload Handling
        services.AddSingleton<IUploadQueue, UploadQueue>(ctx =>
        {
            return new UploadQueue(100);
        });
        services.AddTransient<IImageInfoService, ImageSharpImageInfo>();
        services.AddScoped<IThumbImageService, ThumbImageService>();

        // file storage
        services.AddTransient<IStorageService, LocalStorageService>();
        // TODO: services.AddSingleton<BackgroundUploadWorker>();
        // TODO: services.AddHostedService(p => p.GetRequiredService<BackgroundUploadWorker>());
        services.AddScoped<LocalChacheService>();


        // Caching
        services.AddMemoryCache();
        services.AddScoped<IUserSession, UserSession>();

        // Localization
        services.Configure<LocalizationSettings>(config.GetSection(LocalizationSettings.ConfigName));
        services.AddTransient<LocalizationFileService>();


        // Configure JSON options for OpenAPI
        services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options =>
        {
            options.SerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
            options.SerializerOptions.MaxDepth = 12800; // Increase the max depth to match the HTTP JSON options
            options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        });


        services.AddOpenApi();

        return services;
    }
}