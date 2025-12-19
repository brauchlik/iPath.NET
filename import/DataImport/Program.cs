global using iPath.Domain.Entities;
using iPath.DataImport;
using iPath.API;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using DispatchR.Extensions;


HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

if(System.IO.File.Exists("importsettings.json"))
{
    builder.Configuration.AddJsonFile("importsettings.json");
}
else
{
    throw new Exception("No configuration 'importsetting.json' found");
}

builder.Services.AddDispatchR(cfg => cfg.Assemblies.Add(typeof(iPath.Application.Meta).Assembly));

builder.Services.AddPersistance(builder.Configuration);
builder.Services.AddIPathAuthentication(builder.Configuration);

/*
builder.Services.AddDbContext<iPathDbContext>(cfg =>
{
    cfg.UseSqlite("Data Source=./UI/iPath.Blazor/ipath.db");
});
*/


var cs = builder.Configuration.GetConnectionString("ipath_old");
Console.WriteLine("Data Source: " + cs);
builder.Services.AddDbContext<OldDB>(cfg => cfg.UseMySQL(cs));

builder.Services.Configure<ImportConfig>(options => builder.Configuration.GetSection(nameof(ImportConfig)).Bind(options));

builder.Services.AddScoped<ImportService>();

using IHost host = builder.Build();



if (args.Contains("icdo"))
{
    CodingImport.ImportCodes();
}
else
{ 
    // default import => database
    await DatabaseImport.StartImport(host.Services);
}





