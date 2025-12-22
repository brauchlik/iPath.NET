using DispatchR.Extensions;
using iPath.API.Services;
using iPath.API.Services.Email;
using iPath.API.Services.Notifications;
using iPath.API.Services.Notifications.Processors;
using iPath.API.Services.Storage;
using iPath.API.Services.Thumbnail;
using iPath.API.Services.Uploads;
using iPath.Application.Features.Notifications;
using iPath.Application.Localization;
using iPath.Google;
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
            cfg.Assemblies.Add(typeof(iPath.Application.Meta).Assembly);
        });

        // EF Core and Authentication
        services.AddPersistance(config);
        services.AddIPathAuthentication(config);

        // Authorizations
        services.AddAuthorizationBuilder()
            .AddPolicy("Admin", policy => policy.RequireRole("Admin"))
            .AddPolicy("Developer", policy => policy.RequireRole("Developer"));


        // app config
        services.Configure<iPathConfig>(config.GetSection(iPathConfig.ConfigName));
        //var cfg = new iPathConfig();
        //config.GetSection(iPathConfig.ConfigName).Bind(cfg);

        // Email handling
        var smtp = new SmtpConfig();
        config.GetSection(nameof(SmtpConfig)).Bind(smtp);
        services.Configure<SmtpConfig>(config.GetSection(nameof(SmtpConfig)));
        services.AddSingleton<IEmailQueue, EmailQueue>(ctx =>
        {
            var capacity = config.GetValue<int?>("Norifications:ProcessingQueueCapacity") ?? 100;
            return new EmailQueue(capacity);
        });
        if (smtp.Active)
        {
            // Start mail processing queue only when smtp is set to active
            services.AddTransient<IEmailSender, SmtpEmailSender>();
            services.AddHostedService<EmailQueueWorker>();
        }
        else
        {
            services.AddTransient<IEmailSender, QueueEmailSender>();
        }


        // Notification Handling
        services.AddSingleton<IEventNotificationDispatcherQueue>(ctx => new EventNotificationDispatcherQueue(100));
        services.AddSingleton<INotificationQueue>(ctx => new NotificationQueue(100));
        services.AddHostedService<EventNotificationDispatcher>();
        services.AddScoped<INodeEventProcessor, RootNodeEventProcessor>();


        // Upload Handling
        services.AddSingleton<IUploadQueue, UploadQueue>(ctx =>
        {
            return new UploadQueue(100);
        });
        services.AddTransient<IImageInfoService, ImageSharpImageInfo>();
        services.AddScoped<IThumbImageService, ThumbImageService>();

        // file storage
        services.AddTransient<IMimetypeService, MimetypeService>();
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

        // Google Workspace
        services.AddGoogleServices(config);

        // Configure JSON options for OpenAPI
        services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options =>
        {
            options.SerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
            options.SerializerOptions.MaxDepth = 12800; // Increase the max depth to match the HTTP JSON options
            options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        });

        // SignalR
        services.AddSignalR();
        /*
        services.AddResponseCompression(opts =>
        {
            opts.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
                ["application/octet-stream"]);
        });
        */

        // OpenAPI
        services.AddOpenApi();

        return services;
    }
}