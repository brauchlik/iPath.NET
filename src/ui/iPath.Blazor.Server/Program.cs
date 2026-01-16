using iPath.API;
using iPath.Blazor.Server;
using iPath.Blazor.Server.Components;
using iPath.Blazor.Server.Components.Account;
using iPath.Domain.Config;
using iPath.RazorLib;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using MudBlazor.Services;
using Serilog;
using System.Net;


var builder = WebApplication.CreateBuilder(args);


// Configuration
if (builder.Environment.IsDevelopment())
{
    Console.WriteLine("Reading Application Configuration");
    Console.WriteLine("-------------------------------------------------");
    Console.WriteLine("CONFIG_PATH = " + builder.Configuration["CONFIG_PATH"]);
}
if (!string.IsNullOrEmpty(builder.Configuration["CONFIG_PATH"]))
{
    var cfgFile = System.IO.Path.Combine(builder.Configuration["CONFIG_PATH"]!, "appsettings.json");
    Console.WriteLine("Loading Configuration from {0}", cfgFile);
    if (System.IO.File.Exists(cfgFile))
    {
        builder.Configuration.AddJsonFile(cfgFile);
    }
}

if (builder.Environment.IsDevelopment())
{
    foreach (var s in builder.Configuration.Sources)
    {
        Console.WriteLine("config source: " + s);
        if (s is Microsoft.Extensions.Configuration.Json.JsonConfigurationSource source)
        {
            Console.WriteLine(" - " + source.Path);
        }
    }
    Console.WriteLine("-------------------------------------------------");
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

//Add support to logging with SERILOG
builder.Host.UseSerilog((context, configuration) =>
    configuration.ReadFrom.Configuration(context.Configuration));

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

// Configuration
builder.Services.Configure<iPathConfig>(builder.Configuration.GetSection(iPathConfig.ConfigName));
var cfg = new iPathConfig();
builder.Configuration.GetSection(iPathConfig.ConfigName).Bind(cfg);

// reverse Proxy
if (!string.IsNullOrEmpty(cfg.ReverseProxyAddresse) && IPAddress.TryParse(cfg.ReverseProxyAddresse, out var proxyIP))
{
    builder.Services.Configure<ForwardedHeadersOptions>(o => o.KnownProxies.Add(proxyIP));
}

var app = builder.Build();
var opts = app.Services.GetRequiredService<IOptions<iPathConfig>>();


// Header forwarding for Reverse Proxy Integration
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});


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


// Serve files from external storage folder at request path /files
// - Uses configured LocalDataPath from iPathConfig (used elsewhere by LocalStorageService).
// - Falls back to no-op and logs a warning if folder is not configured or missing.
var externalFilesPath = opts.Value?.TempDataPath;
if (!string.IsNullOrWhiteSpace(externalFilesPath))
{
    try
    {
        if (Directory.Exists(externalFilesPath))
        {
            var provider = new PhysicalFileProvider(Path.GetFullPath(externalFilesPath));
            var contentTypeProvider = new FileExtensionContentTypeProvider();
            contentTypeProvider.Mappings[".svs"] = "application/octet-stream";
            // Optionally: add unknown mappings or overrides here, e.g. contentTypeProvider.Mappings[".bin"] = "application/octet-stream";

            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = provider,
                RequestPath = "/files",
                ContentTypeProvider = contentTypeProvider,
                ServeUnknownFileTypes = true, // allow binary files with unknown extensions
                /*
                OnPrepareResponse = ctx =>
                {
                    ctx.Context.Response.Headers.Append("Cache-Control", "no-cache, no-store, must-revalidate");
                    ctx.Context.Response.Headers.Append("Pragma", "no-cache");
                    ctx.Context.Response.Headers.Append("Expires", "0");
                }
                */
            });
        }
        else
        {
            var logger = app.Services.GetRequiredService<ILogger<Program>>();
            logger.LogWarning("Configured TempDataPath '{path}' does not exist; /files will not be available.", externalFilesPath);
        }
    }
    catch (Exception ex)
    {
        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        logger.LogWarning(ex, "Failed to configure static file serving for '{path}'", externalFilesPath);
    }
}
else
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogWarning("TempDataPath is not configured; /files will not be available.");
}


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
