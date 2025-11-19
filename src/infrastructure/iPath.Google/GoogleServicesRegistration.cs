using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace iPath.Google;

public static class GoogleServicesRegistration
{
    public static IServiceCollection AddGoogleServices(this IServiceCollection services, IConfiguration config)
    {
        return services;
    }
}