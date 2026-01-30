using iPath.Blazor.Server;
using iPath.Domain.Config;
using iPath.RazorLib;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;
using System.Runtime.CompilerServices;

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


builder.Services.AddMudServices();

builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthenticationStateDeserialization();

builder.Services.AddTransient<baseAuthDelegationHandler>();

var baseAddress = cfg.BaseAddress ?? builder.HostEnvironment.BaseAddress;
Console.WriteLine("Blazor WASM starting with Base: " + baseAddress);

await builder.Services.AddRazorLibServices(baseAddress, true);

Console.WriteLine("Blazor WASM RunAsync()");
await builder.Build().RunAsync();
