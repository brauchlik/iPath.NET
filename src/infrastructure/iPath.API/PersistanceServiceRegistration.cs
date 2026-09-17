using iPath.API.Services;
using iPath.API.Sqlite;
using iPath.Application.AI;
using iPath.Application.Features.Admin;
using iPath.Application.Features.Notifications;
using iPath.Application.Localization;
using iPath.Database.EFCore.AI;
using iPath.Domain.Config;
using iPath.EF.Core.Database;
using iPath.EF.Core.FeatureHandlers.Emails;
using iPath.EF.Core.FeatureHandlers.Groups;
using iPath.EF.Core.FeatureHandlers.Notifications;
using iPath.Google;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OllamaSharp;


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
                var cs = config.GetConnectionString(DBProvider.Sqlite.Name) ?? "ipath.db";
                cfg.UseSqlite(cs, x => x.MigrationsAssembly(DBProvider.Sqlite.Assembly));
                cfg.AddInterceptors(new SqliteWalInterceptor());  
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

        // Register AI Services
        services.AddScoped<IPromptContextResolver, PromptContextResolver>();
        services.AddScoped<IAiExtractionService, AiExtractionService>();
        services.AddScoped<ISemanticSearchService, SemanticSearchService>();
        services.AddSingleton<IAiExtractionQueue, AiExtractionQueue>();
        services.AddHostedService<AiExtractionWorker>();
        services.AddHostedService<AiExtractionBackfill>();

        // Register dynamic IChatClient and IEmbeddingGenerator based on configured provider
        var aiSection = config.GetSection(AiSettingsConfig.ConfigName);
        var aiCfg = new AiSettingsConfig();
        aiSection.Bind(aiCfg);
        var aiProvider = aiCfg.Provider.ToLowerInvariant();

        switch (aiProvider)
        {
            case "openai":
                {
                    var key = aiSection.GetValue<string>("OpenAI:ApiKey") ?? "";
                    var chatModel = aiSection.GetValue<string>("OpenAI:ChatModel") ?? "gpt-4o";
                    var embedModel = aiSection.GetValue<string>("OpenAI:EmbeddingModel") ?? "text-embedding-3-small";
                    var client = new global::OpenAI.OpenAIClient(new global::System.ClientModel.ApiKeyCredential(key));
                    services.AddScoped<IChatClient>(sp => client.GetChatClient(chatModel).AsIChatClient());
                    services.AddScoped<IEmbeddingGenerator<string, Embedding<float>>>(sp => client.GetEmbeddingClient(embedModel).AsIEmbeddingGenerator());
                }
                break;

            case "google":
                {
                    var key = aiSection.GetValue<string>("Google:ApiKey") ?? "";
                    var chatModel = aiSection.GetValue<string>("Google:ChatModel") ?? "gemini-1.5-flash";
                    var embedModel = aiSection.GetValue<string>("Google:EmbeddingModel") ?? "text-embedding-004";
                    var client = new global::OpenAI.OpenAIClient(new global::System.ClientModel.ApiKeyCredential(key), new global::OpenAI.OpenAIClientOptions
                    {
                        Endpoint = new Uri("https://generativelanguage.googleapis.com/v1beta/openai/")
                    });
                    services.AddScoped<IChatClient>(sp => client.GetChatClient(chatModel).AsIChatClient());
                    services.AddScoped<IEmbeddingGenerator<string, Embedding<float>>>(sp => client.GetEmbeddingClient(embedModel).AsIEmbeddingGenerator());
                }
                break;

            case "ollama":
            default:
                {
                    var baseUriStr = aiSection.GetValue<string>("Ollama:BaseUri") ?? "http://localhost:11434/";
                    var chatModel = aiSection.GetValue<string>("Ollama:ChatModel") ?? "llama3";
                    var embedModel = aiSection.GetValue<string>("Ollama:EmbeddingModel") ?? "nomic-embed-text";
                    var baseUri = new Uri(baseUriStr);

                    services.AddScoped<IChatClient>(sp =>
                        new OllamaApiClient(baseUri, chatModel));
                    services.AddScoped<IEmbeddingGenerator<string, Embedding<float>>>(sp =>
                        new OllamaApiClient(baseUri, embedModel));
                }
                break;
        }

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
                cfg.AddInterceptors(new SqliteWalInterceptor());
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

    /// <summary>
    /// Pushes new/updated keys from the shipped LocalizationSettings.DefaultsRoot baseline into
    /// the configured LocalesRoot live store - additive only, see LocalizationKeyScanner.PushDefaults.
    /// No-op unless LocalizationSettings.AutoUpdate is set (same opt-in shape as DbAutoMigrate).
    /// </summary>
    public static async Task UpdateTranslations(this IHost host)
    {
        using var scope = host.Services.CreateScope();
        var opts = scope.ServiceProvider.GetRequiredService<IOptions<LocalizationSettings>>().Value;
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<LocalizationFileService>>();

        if (!opts.AutoUpdate) return;

        var defaultsService = new LocalizationFileService(
            Options.Create(new LocalizationSettings { LocalesRoot = opts.DefaultsRoot, SupportedCultures = opts.SupportedCultures }),
            logger);
        var targetService = new LocalizationFileService(
            Options.Create(new LocalizationSettings { LocalesRoot = opts.LocalesRoot, SupportedCultures = opts.SupportedCultures }),
            logger);

        foreach (var locale in opts.SupportedCultures)
        {
            try
            {
                var defaults = defaultsService.GetTranslationData(locale);
                var target = targetService.GetTranslationData(locale);
                var (added, filled) = LocalizationKeyScanner.PushDefaults(defaults, target);

                if (added.Count > 0 || filled.Count > 0)
                {
                    targetService.SaveTranslation(target);
                    logger.LogInformation("Translations updated for {Locale}: {Added} new, {Filled} filled from shipped defaults", locale, added.Count, filled.Count);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error pushing shipped default translations for locale {Locale}", locale);
            }
        }
    }
}


public record DBProvider(string Name, string Assembly)
{
    public static DBProvider InMemory = new(nameof(InMemory), null); // InMemory DB has no migrations
    public static DBProvider Sqlite = new(nameof(Sqlite), typeof(iPath.EF.Sqlite.Marker).Assembly.GetName().Name!);
    public static DBProvider Postgres = new(nameof(Postgres), typeof(iPath.EF.Postgres.Marker).Assembly.GetName().Name!);
    // public static DBProvider SqlServer = new(nameof(SqlServer), typeof(iPath.EF.SqlServer.Marker).Assembly.GetName().Name!);
}