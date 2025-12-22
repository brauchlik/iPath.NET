using iPath.API;
using iPath.Blazor.Server;
using iPath.Blazor.Server.Components;
using iPath.Blazor.Server.Components.Account;
using iPath.Domain.Config;
using iPath.RazorLib;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MudBlazor.Services;


var builder = WebApplication.CreateBuilder(args);

// Configuration
if (!string.IsNullOrEmpty(builder.Configuration["CONFIG_PATH"]))
{
    var cfgFile = System.IO.Path.Combine(builder.Configuration["CONFIG_PATH"]!, "appsettings.json");
    Console.WriteLine("Loading Configuration from {0}", cfgFile);
    if (System.IO.File.Exists(cfgFile))
    {
        builder.Configuration.AddJsonFile(cfgFile);
    }
}


builder.AddServiceDefaults();

// builder.WebHost.UseStaticWebAssets();

// Add MudBlazor services
builder.Services.AddMudServices();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents()
    .AddAuthenticationStateSerialization();

// Authentication Frontend
builder.Services.AddScoped<IdentityUserAccessor>();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();
builder.Services.AddCascadingAuthenticationState();

// TODO: move to infrastructure
builder.Services.AddScoped<IEmailSender<User>, IdentityQueuedSender>();

// API => adds then adpplication services, persistance, authentication, etc ... 
builder.Services.AddIPathAPI(builder.Configuration);

// TODO: make configurable (development only)
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

var baseAddress = builder.Configuration["BaseAddress"] ?? "http://localhost:5000/";
builder.Services.AddRazorLibServices(baseAddress, false);

// testing SSE
builder.Services.AddSingleton<NotificationService>();

builder.Services.AddAntiforgery();


builder.Services.AddTransient<baseAuthDelegationHandler, ForwardCookiesHandler>();


builder.Services.Configure<iPathConfig>(builder.Configuration.GetSection(iPathConfig.ConfigName));



var app = builder.Build();
var opts = app.Services.GetRequiredService<IOptions<iPathConfig>>();

app.MapDefaultEndpoints();


// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

// DB Migrations & Seeding
await app.UpdateDatabase();


// Configure static file caching
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        ctx.Context.Response.Headers.Append(
            "Cache-Control", "no-cache, no-store, must-revalidate");
        ctx.Context.Response.Headers.Append(
            "Pragma", "no-cache");
        ctx.Context.Response.Headers.Append(
            "Expires", "0");
    }
});



// app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(iPath.Blazor.Client._Imports).Assembly)
    .AddAdditionalAssemblies(typeof(iPath.RazorLib.Meta).Assembly);

app.MapIPathApi();

// Add additional endpoints required by the Identity /Account Razor components.
app.MapAdditionalIdentityEndpoints();

app.Run();
