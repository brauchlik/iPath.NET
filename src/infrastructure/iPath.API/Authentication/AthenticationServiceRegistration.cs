using iPath.API.Authentication;
using iPath.EF.Core.Database;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace iPath.API;

public static class AthenticationServiceRegistration
{
    public static IServiceCollection AddIPathAuthentication(this IServiceCollection services, IConfiguration config)
    {
        var opts = new AuthOptions();
        config.GetSection(nameof(AuthOptions)).Bind(opts);

        services.AddAuthentication(options =>
        {
            options.DefaultScheme = IdentityConstants.ApplicationScheme;
            options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
        })
            .AddIdentityCookies();

        // Identity Services
        services.AddIdentityCore<User>(options => {
            options.SignIn.RequireConfirmedAccount = opts.RequireConfirmedAccount;
            options.User.RequireUniqueEmail = opts.RequireUniqueEmail;
            options.User.AllowedUserNameCharacters = string.IsNullOrEmpty(opts.AllowedUserNameCharacters) ? chars : opts.AllowedUserNameCharacters;
        })
            .AddRoles<Role>()
            .AddEntityFrameworkStores<iPathDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();


        return services;
    }


    const string chars = @"+-.0123456789@ABCDEFGHIJKLMNOPQRSTUVWXYZ_abcdefghijklmnopqrstuvwxyzäçèéïöüčėţūŽžơưҲị";
}
