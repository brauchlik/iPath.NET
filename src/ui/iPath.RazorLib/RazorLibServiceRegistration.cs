using iPath.Blazor.Componenents.Admin.Communities;
using iPath.Blazor.Componenents.Admin.Groups;
using iPath.Blazor.Componenents.Admin.Users;
using iPath.Blazor.Componenents.Groups;
using iPath.Blazor.Componenents.Nodes;
using iPath.Blazor.Componenents.Users;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Refit;
using System.Data;

namespace iPath.RazorLib;

public static class RazorLibServiceRegistration
{
    public static IServiceCollection AddRazorLibServices(this IServiceCollection services, string baseAddress)
    {
        services.AddRefitClient<IPathApi>()
            .ConfigureHttpClient(c => c.BaseAddress = new Uri(baseAddress));

        services.AddMemoryCache();
        services.AddViewModels();

        // Localization
        services.AddSingleton<LocalizationService>();
        // register the same singleton for use as IStringLocalizer
        services.AddSingleton<IStringLocalizer>(p => p.GetRequiredService<LocalizationService>());
        services.AddLocalization();

        return services;
    }


    private static IServiceCollection AddViewModels(this IServiceCollection services)
    {
        // admin
        services.AddScoped<CommunityAdminViewModel>();
        services.AddScoped<GroupAdminViewModel>();
        services.AddScoped<UserAdminViewModel>();

        // users
        services.AddScoped<GroupListViewModel>();
        services.AddScoped<GroupIndexViewModel>();
        services.AddScoped<NodeViewModel>();
        services.AddScoped<UserViewModel>();

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
