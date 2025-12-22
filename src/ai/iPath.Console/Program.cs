using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using System.Data.Common;


var builder =  Host.CreateApplicationBuilder(args);
builder.Configuration.AddJsonFile("appsettings.json");

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
Console.WriteLine("connecting to :" + ollamaEndpoint);

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
    return kb.Build();
});



var host = builder.Build();

var kernel = host.Services.GetService<Kernel>();
var chatService = kernel.Services.GetService<IChatCompletionService>();


string input = string.Empty;
while (input != "quit")
{
    Console.Write("You: ");
    input = Console.ReadLine();
    Console.WriteLine(await kernel.InvokePromptAsync(input));
}

