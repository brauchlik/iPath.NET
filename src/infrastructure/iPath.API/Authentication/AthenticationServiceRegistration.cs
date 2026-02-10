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
        services.Configure<AuthOptions>(config.GetSection(nameof(AuthOptions)));

        var authBuilder = services.AddAuthentication(options =>
        {
            options.DefaultScheme = IdentityConstants.ApplicationScheme;
            options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
        });

        authBuilder.AddIdentityCookies();

        // Identity Services
        services.AddIdentityCore<User>(options => {
            options.SignIn.RequireConfirmedAccount = opts.RequireConfirmedAccount;
            options.User.RequireUniqueEmail = opts.RequireUniqueEmail;
            options.User.AllowedUserNameCharacters = string.IsNullOrEmpty(opts.AllowedUserNameCharacters) ? chars : opts.AllowedUserNameCharacters;
        })
            .AddRoles<Role>()
            .AddEntityFrameworkStores<iPathDbContext>()
            .AddSignInManager<IPathSignInManager>()
            .AddDefaultTokenProviders();


        // external logins
        if (opts.Google.IsActive)
        {
            authBuilder.AddGoogle("Google", options =>
            {
                options.ClientId = opts.Google.ClientId;
                options.ClientSecret = opts.Google.ClientSecret;
                options.SaveTokens = true;
            });
        }
        if (opts.Microsoft.IsActive)
        {
            authBuilder.AddMicrosoftAccount("Microsoft", options =>
            {
                options.ClientId = opts.Microsoft.ClientId;
                options.ClientSecret = opts.Microsoft.ClientSecret;
                options.SaveTokens = true;
            });
        }


        return services;
    }


    const string chars = @"+-.0123456789@ABCDEFGHIJKLMNOPQRSTUVWXYZ_abcdefghijklmnopqrstuvwxyzäçèéïöüčėţūŽžơưҲị";
}
