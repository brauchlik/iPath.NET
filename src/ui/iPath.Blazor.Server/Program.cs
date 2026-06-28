using iPath.API;
using iPath.Application.Localization;
using iPath.Blazor.Server.Components;
using iPath.Blazor.Server.Components.Account;
using iPath.Domain.Config;
using iPath.RazorLib;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using MudBlazor.Services;
using Serilog;
using System.Net;
using Serilog.Enrichers.Span;
using Serilog.Exceptions;
using Serilog.Exceptions.Core;
using Serilog.Exceptions.Destructurers;
using Serilog.Exceptions.EntityFrameworkCore.Destructurers;
using Serilog.Exceptions.Refit.Destructurers;


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


// Observability 
//builder.Logging.AddOTLPLogging();
//builder.Services.AddObservability();
builder.AddServiceDefaults();


// builder.WebHost.UseStaticWebAssets();

// Add MudBlazor services
builder.Services.AddMudServices();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents()
    .AddAuthenticationStateSerialization();

// Add support to logging with SERILOG
builder.Host.UseSerilog((context, configuration) =>
    configuration.ReadFrom.Configuration(context.Configuration)
        .Enrich.WithSpan()
    .Enrich.WithExceptionDetails(new DestructuringOptionsBuilder()
        .WithDefaultDestructurers()
        .WithDestructurers(new IExceptionDestructurer[]
        {
            new DbUpdateExceptionDestructurer(),
            new ApiExceptionDestructurer()
        }))
    .Enrich.WithDemystifiedStackTraces());

builder.Services.AddHttpLogging();

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

// Server Configuration
builder.Services.Configure<iPathConfig>(builder.Configuration.GetSection(iPathConfig.ConfigName));
var cfg = new iPathConfig();
builder.Configuration.GetSection(iPathConfig.ConfigName).Bind(cfg);

// Client Configuration
builder.Services.Configure<iPathClientConfig>(builder.Configuration.GetSection(iPathClientConfig.ConfigName));
var clcfg = new iPathClientConfig();
builder.Configuration.GetSection(iPathClientConfig.ConfigName).Bind(clcfg);


var baseAddress = clcfg.BaseAddress ?? "http://localhost:5000/";
await builder.Services.AddRazorLibServices(baseAddress, false);

builder.Services.AddAntiforgery();


// reverse Proxy
if (!string.IsNullOrEmpty(cfg.ReverseProxyAddresse) && IPAddress.TryParse(cfg.ReverseProxyAddresse, out var proxyIP))
{
    builder.Services.Configure<ForwardedHeadersOptions>(o => o.KnownProxies.Add(proxyIP));
}


builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.Limits.MaxRequestBodySize = clcfg.MaxFileSizeBytes;
});


// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("CorsPolicy", policy =>
    {
        policy.AllowAnyOrigin() // TODO: restrict to URL in production
              .WithMethods("GET", "OPTIONS")
              .WithHeaders("Range", "Authorization", "X-Requested-With")
              .WithExposedHeaders("Content-Range", "Accept-Ranges", "Content-Length");
    });
});




var app = builder.Build();
app.UseHttpLogging();
var opts = app.Services.GetRequiredService<IOptions<iPathConfig>>();


await app.InitStorageAsync();

// Check Old DB (sync import) connection on startup
var syncCs = app.Services.GetRequiredService<IConfiguration>().GetConnectionString("ipath_old");
if (!string.IsNullOrEmpty(syncCs))
{
    var log = app.Services.GetRequiredService<ILogger<Program>>();
    try
    {
        using var conn = new MySqlConnector.MySqlConnection(syncCs);
        await conn.OpenAsync();
        log.LogInformation("Old DB (ipath_old) connection OK — sync import available");

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT default_character_set_name FROM information_schema.SCHEMATA WHERE schema_name = DATABASE()";
        var charset = await cmd.ExecuteScalarAsync();
        log.LogInformation("Old DB charset: {Charset}", charset);
    }
    catch (Exception ex)
    {
        log.LogWarning(ex, "Old DB (ipath_old) is configured but not reachable: {Message}", ex.Message);
        Console.Error.WriteLine("WARNING: Old MySQL DB (ipath_old) is configured but not reachable.");
        Console.Error.WriteLine($"  {ex.Message}");
    }
}

// DI for Extensions
app.Services.InitComponenetsExtensions();


app.UseCors("CorsPolicy");

var l10nSettings = app.Services.GetRequiredService<IOptions<iPath.Application.Localization.LocalizationSettings>>().Value;
var supportedCultures = l10nSettings.SupportedCultures;
var localizationOptions = new RequestLocalizationOptions()
    .SetDefaultCulture("en")
    .AddSupportedCultures(supportedCultures)
    .AddSupportedUICultures(supportedCultures);

app.UseRequestLocalization(localizationOptions);

// Preload ALL localization data into the Singleton cache at startup
var srvLoc = app.Services.GetRequiredService<iPath.Blazor.ServiceLib.Services.StringLocalizerService>();
foreach (var culture in supportedCultures)
{
    await srvLoc.LoadTranslationData(culture);
}

app.UseAuthentication();
app.UseAuthorization();

// Header forwarding for Reverse Proxy Integration
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

// Health Checks, etc (Aspire)
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
            // contentTypeProvider.Mappings[".svs"] = "application/octet-stream";
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

            // Fallback for missing WSI files under /files path (unzip on demand)
            app.Use(async (context, next) =>
            {
                var path = context.Request.Path.Value;
                if (path != null && path.StartsWith("/files/", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length > 1)
                    {
                        var target = parts[1];
                        Guid? docId = null;
                        if (target.EndsWith(".dzi", StringComparison.OrdinalIgnoreCase) && Guid.TryParse(target[..^4], out var id1))
                        {
                            docId = id1;
                        }
                        else if (target.EndsWith("_files", StringComparison.OrdinalIgnoreCase) && Guid.TryParse(target[..^6], out var id2))
                        {
                            docId = id2;
                        }

                        if (docId.HasValue)
                        {
                            var mediator = context.RequestServices.GetRequiredService<DispatchR.IMediator>();
                            var res = await mediator.Send(new iPath.Application.Features.GetDocumentFileQuery(docId.Value), context.RequestAborted);
                            if (res != null && !res.NotFound && !res.AccessDenied && res.TempFile != null)
                            {
                                var filename = res.Info?.Filename;
                                var isZip = filename?.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) == true
                                            || filename?.EndsWith(".dzi", StringComparison.OrdinalIgnoreCase) == true
                                            || filename?.EndsWith(".vsi", StringComparison.OrdinalIgnoreCase) == true;
                                if (isZip && File.Exists(res.TempFile))
                                {
                                    try
                                    {
                                        System.IO.Compression.ZipFile.ExtractToDirectory(res.TempFile, externalFilesPath, overwriteFiles: true);
                                    }
                                    catch (Exception ex)
                                    {
                                        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
                                        logger.LogError(ex, "Failed to unzip cache file {Path} to {Temp}", res.TempFile, externalFilesPath);
                                    }
                                }
                                
                                context.Response.Redirect(context.Request.Path + context.Request.QueryString);
                                return;
                            }
                        }
                    }
                }
                await next(context);
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

app.MapIPathApi(builder.Configuration);

// Add additional endpoints required by the Identity /Account Razor components.
app.MapAdditionalIdentityEndpoints();

app.MapGet("/api/localization/set", (string culture, string redirectUri, HttpContext httpContext) =>
{
    if (!string.IsNullOrEmpty(culture))
    {
        httpContext.Response.Cookies.Append(
            CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
            new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1), IsEssential = true }
        );
    }
    return Results.LocalRedirect(redirectUri ?? "/");
});

app.MapGet("/api/localization/current", (HttpContext httpContext, IOptions<LocalizationSettings> l10n) =>
{
    var feature = httpContext.Features.Get<IRequestCultureFeature>();
    var locale = feature?.RequestCulture.UICulture.TwoLetterISOLanguageName ?? "en";
    return Results.Ok(new { currentCulture = locale, supportedCultures = l10n.Value.SupportedCultures, cultureDisplayNames = l10n.Value.CultureDisplayNames });
}).AllowAnonymous();

app.Run();
