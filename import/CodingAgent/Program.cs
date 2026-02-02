using CodingAgent;
using CodingAgent.Features.Coding;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;


HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

if (System.IO.File.Exists("appsettings.json"))
{
    builder.Configuration.AddJsonFile("appsettings.json");
}
else
{
    throw new Exception("No configuration 'appsettings.json' found");
}

builder.Services.RegisterServices(builder.Configuration);
using IHost host = builder.Build();

await CreateMissingBodySites(host);
return 0;



static async Task CreateMissingBodySites(IHost host)
{
    using var scope = host.Services.CreateScope();
    var coder = scope.ServiceProvider.GetRequiredService<BodySiteCoding>();
    await coder.CreateMissingCodes();
}
