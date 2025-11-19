using iPath.EF.Core.Database;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace iPath.API;

public static class AthenticationServiceRegistration
{
    public static IServiceCollection AddIPathAuthentication(this IServiceCollection services, IConfiguration conffig)
    {
        services.AddAuthentication(options =>
        {
            options.DefaultScheme = IdentityConstants.ApplicationScheme;
            options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
        })
            .AddIdentityCookies();

        // Identity Services
        services.AddIdentityCore<User>(options => {
            options.SignIn.RequireConfirmedAccount = true;
            options.User.RequireUniqueEmail = false;
            options.User.AllowedUserNameCharacters = chars;
        })
            .AddRoles<Role>()
            .AddEntityFrameworkStores<iPathDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();


        return services;
    }


    const string chars = @"+-.0123456789@ABCDEFGHIJKLMNOPQRSTUVWXYZ_abcdefghijklmnopqrstuvwxyzäçèéïöüčėţūŽžơưҲị";
}
