using iPath.Blazor.Server.Components;
using iPath.Blazor.Server.Components.Account;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;
using iPath.API;
using iPath.RazorLib;


var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseStaticWebAssets();

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


builder.Services.AddRazorLibServices("http://localhost:5000/");

// testing SSE
builder.Services.AddSingleton<NotificationService>();


var app = builder.Build();


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
app.UpdateDatabase();


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
