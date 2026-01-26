using iPath.Application.Fhir;
using iPath.Application.Localization;
using iPath.Blazor.Componenents.Admin.Communities;
using iPath.Blazor.Componenents.Admin.Groups;
using iPath.Blazor.Componenents.Admin.Questionnaires;
using iPath.Blazor.Componenents.Admin.Users;
using iPath.Blazor.Componenents.Communities;
using iPath.Blazor.Componenents.Shared;
using iPath.Blazor.Componenents.Users;
using iPath.Blazor.Server;
using iPath.Blazor.ServiceLib.Fhir;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Translations;
using Refit;
using System.Data;
using System.Text.Json;

namespace iPath.RazorLib;

public static class RazorLibServiceRegistration
{
    public static async Task<IServiceCollection> AddRazorLibServices(this IServiceCollection services, string baseAddress, bool WasmClient)
    {
        services.AddMudTranslations();


        // Refit client with json serialization options => enums as int
        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString,
        };
        var refitSetting = new RefitSettings { 
            ContentSerializer = new SystemTextJsonContentSerializer(jsonOptions) 
        };
        services.AddRefitClient<IPathApi>(refitSetting)
            .ConfigureHttpClient(c => c.BaseAddress = new Uri(baseAddress))
            .AddHttpMessageHandler<baseAuthDelegationHandler>();

        services.AddMemoryCache();
        services.AddSingleton<GroupCache>();
        services.AddViewModels();

        // Localization
        if (WasmClient)
        {
            // get locaization data from API
            services.AddSingleton<ILocalizationDataProvider, ApiLocalizationProvider>();
        }
        else
        {
            services.AddSingleton<ILocalizationDataProvider, FileLocalizaitonProvider>();
        }

        services.AddSingleton<StringLocalizerService>();
        // register the same singleton for use as IStringLocalizer
        services.AddSingleton<IStringLocalizer>(p => p.GetRequiredService<StringLocalizerService>());
        services.AddLocalization();


        // FHIR: questionnaires & coding
        services.AddSingleton<QuestionnaireCacheClient>();

        services.AddHttpClient("Fhir", cfg => cfg.BaseAddress = new Uri(baseAddress + "api/v1/fhir/"));
        services.AddSingleton<IFhirDataLoader, HttpFhirDataLoader>();
        services.AddKeyedSingleton<CodingService>("icdo", (sp, key) =>
        {
            return new CodingService(sp, "icdo");
        });


        // DI for Extensions
        var sp = services.BuildServiceProvider();
        DocumentExtensions.Initialize(sp);
        QuestionnaireExtension.Initialize(sp);


        services.AddScoped<AppState>();

        return services;
    }


    private static IServiceCollection AddViewModels(this IServiceCollection services)
    {
        // admin
        services.AddScoped<CommunityAdminViewModel>();
        services.AddScoped<GroupAdminViewModel>();
        services.AddScoped<UserAdminViewModel>();
        services.AddScoped<QuestionnaireAdminViewModel>();

        // users
        services.AddScoped<CommunityViewModel>();
        services.AddScoped<GroupListViewModel>();
        services.AddScoped<GroupViewModel>();
        services.AddScoped<ServiceRequestListViewModel>();
        services.AddScoped<ServiceRequestViewModel>();
        services.AddScoped<DocumentViewModel>();
        services.AddScoped<UserViewModel>();
        services.AddScoped<QuestionnairesViewModel>();

        return services;
    }

    private static IServiceCollection AddViewModelsDynamic(this IServiceCollection services)
    {
        // Register all IViewModel
        var types = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(s => s.GetTypes())
            .Where(p => typeof(IViewModel).IsAssignableFrom(p) && !p.IsAbstract);
        foreach (var vm in types)
        {
            services.Add(new ServiceDescriptor(vm.GetType(), vm, ServiceLifetime.Scoped));
        }

        return services;
    }


}
