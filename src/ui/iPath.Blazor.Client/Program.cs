using iPath.Application.Localization;
using iPath.Domain.Config;
using iPath.RazorLib;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;
using System.Globalization;
using System.Text.Json;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// get client config from api
var cfg = new iPathClientConfig();
try
{
    var http = new HttpClient()
    {
        BaseAddress = new Uri(builder.HostEnvironment.BaseAddress),
        Timeout = TimeSpan.FromSeconds(5)
    };

    using var response = await http.GetAsync("api/v1/config");
    using var stream = await response.Content.ReadAsStreamAsync();
    builder.Configuration.AddJsonStream(stream);

    builder.Services.Configure<iPathClientConfig>(builder.Configuration.GetSection(iPathClientConfig.ConfigName));
    builder.Configuration.GetSection(iPathClientConfig.ConfigName).Bind(cfg);
}
catch (Exception ex)
{
    Console.WriteLine(ex.ToString());
    // throw ex;
}

// fetch per-session culture from server (reads .AspNetCore.Culture cookie)
string currentCulture = "en";
try
{
    using var http = new HttpClient()
    {
        BaseAddress = new Uri(builder.HostEnvironment.BaseAddress),
        Timeout = TimeSpan.FromSeconds(5)
    };

    using var cultureResp = await http.GetAsync("api/localization/current");
    using var cultureStream = await cultureResp.Content.ReadAsStreamAsync();
    var cultureData = await JsonSerializer.DeserializeAsync<CultureResponse>(cultureStream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    currentCulture = cultureData?.CurrentCulture ?? "en";
}
catch (Exception ex)
{
    Console.WriteLine($"Error fetching culture: {ex}");
}

// set culture for ENTIRE app lifetime BEFORE building
var ci = CultureInfo.GetCultureInfo(currentCulture);
CultureInfo.DefaultThreadCurrentCulture = ci;
CultureInfo.DefaultThreadCurrentUICulture = ci;

builder.Services.AddMudServices();

builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthenticationStateDeserialization();

var baseAddress = cfg.BaseAddress ?? builder.HostEnvironment.BaseAddress;
Console.WriteLine("Blazor WASM starting with Base: " + baseAddress);

await builder.Services.AddRazorLibServices(baseAddress, true);
builder.Services.AddScoped<iPath.Application.Features.CaseRoom.ICaseRoomSyncService, iPath.Blazor.Componenents.CaseRoom.HttpCaseRoomSyncService>();
builder.Services.AddScoped<iPath.Application.Features.CaseRoom.ICaseRoomSyncReceiver, iPath.Blazor.Componenents.CaseRoom.HttpCaseRoomSyncReceiver>();

Console.WriteLine("Blazor WASM RunAsync()");

var app = builder.Build();

// DI for Extensions
app.Services.InitComponenetsExtensions();

// Preload localization data for WebAssembly client (uses correct culture from DefaultThreadCurrentUICulture)
try
{
    var srvLoc = app.Services.GetRequiredService<ITranslationLoader>();
    await srvLoc.LoadTranslationData(ci.TwoLetterISOLanguageName);
}
catch (Exception ex)
{
    Console.WriteLine($"Error preloading localization in WASM: {ex}");
}

await app.RunAsync();

internal record CultureResponse(string CurrentCulture, string[] SupportedCultures, Dictionary<string, string> CultureDisplayNames);