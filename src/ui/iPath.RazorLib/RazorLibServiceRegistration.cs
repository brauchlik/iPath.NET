using iPath.Blazor.Componenents.Admin;
using iPath.Blazor.Componenents.Admin.Communities;
using iPath.Blazor.Componenents.Admin.Groups;
using iPath.Blazor.Componenents.Admin.Questionnaires;
using iPath.Blazor.Componenents.Admin.Users;
using iPath.Blazor.Componenents.Communities;
using iPath.Blazor.Componenents.Nodes;
using iPath.Blazor.Componenents.Questionaiires;
using iPath.Blazor.Componenents.Shared;
using iPath.Blazor.Componenents.Users;
using iPath.Blazor.Server;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Refit;
using System.Data;
using System.Text.Json;

namespace iPath.RazorLib;

public static class RazorLibServiceRegistration
{
    public static IServiceCollection AddRazorLibServices(this IServiceCollection services, string baseAddress)
    {
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
        services.AddViewModels();

        // Localization
        services.AddSingleton<LocalizationService>();
        // register the same singleton for use as IStringLocalizer
        services.AddSingleton<IStringLocalizer>(p => p.GetRequiredService<LocalizationService>());
        services.AddLocalization();

        services.AddSingleton<QuestionnaireCache>();

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
        services.AddScoped<GroupIndexViewModel>();
        services.AddScoped<NodeListViewModel>();
        services.AddScoped<NodeViewModel>();
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
