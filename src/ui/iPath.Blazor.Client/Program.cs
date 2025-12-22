using iPath.Blazor.Server;
using iPath.RazorLib;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddMudServices();

builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthenticationStateDeserialization();

builder.Services.AddTransient<baseAuthDelegationHandler>();

builder.Services.AddRazorLibServices(builder.HostEnvironment.BaseAddress, true);

await builder.Build().RunAsync();
