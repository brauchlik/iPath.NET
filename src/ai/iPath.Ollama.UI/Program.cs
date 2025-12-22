using iPath.API;
using iPath.EF.Core.Database;
using iPath.Ollama.UI.Components;
using iPath.Ollama.UI.Plugins;
using Microsoft.SemanticKernel;
using MudBlazor.Services;
using System.Data.Common;


var builder = WebApplication.CreateBuilder(args);

// Add MudBlazor services
builder.Services.AddMudServices();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();


builder.Services.AddIPathAPI(builder.Configuration);

builder.Services.AddHttpClient("ollama", c =>
{
    c.BaseAddress = new Uri("http://ollama");
});

var cs = builder.Configuration.GetConnectionString("mistral");
var csBuilder = new DbConnectionStringBuilder { ConnectionString = cs };

if (!csBuilder.TryGetValue("Endpoint", out var ollamaEndpoint))
{
    throw new InvalidDataException("Ollama connection string is not properly configured.");
}


var lights = new List<LightModel>()
   {
      new LightModel { Id = 1, Name = "Bathroom Lamp", IsOn = false },
      new LightModel { Id = 2, Name = "Kitchen Lamp", IsOn = false },
      new LightModel { Id = 3, Name = "Bedroom Lamp", IsOn = true }
   };

builder.Services.AddSingleton(lights);

builder.Services.AddTransient(sp =>
{
    var kernelBuilder = Kernel.CreateBuilder().AddOllamaChatCompletion("mistral", new Uri((string)ollamaEndpoint));
    kernelBuilder.Services.AddSingleton(lights);
    kernelBuilder.Plugins.AddFromType<HomePlugin>();

    /*
    kernelBuilder.Services.AddScoped<iPathDbContext>();
    kernelBuilder.Plugins.AddFromType<UserPlugin>();
    */

    kernelBuilder.Services.AddHttpClient();
    return kernelBuilder.Build();
});

/*
builder.Services.AddSingleton(sp =>
{
    var http = sp.GetRequiredService<IHttpClientFactory>().CreateClient("ollama");

    IKernelBuilder kb = Kernel.CreateBuilder();
#pragma warning disable SKEXP0070
    kb.AddOllamaChatCompletion(
        modelId: "mistral",
        endpoint: new Uri((string)ollamaEndpoint)
    );
#pragma warning restore SKEXP0070

    // kb.Plugins.AddFromObject(new UserPlugin(sp.GetRequiredService<iPathDbContext>()));
    kb.Plugins.AddFromType<HomePlugin>();

    return kb.Build();
});
*/


var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}


app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
