using iPath.Application.Coding;
using iPath.EF.Core.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.Ollama;

namespace CodingAgent.Features.Coding;

public class BodySiteCoding(iPathDbContext db, ILogger<BodySiteCoding> logger, CodingPlugin plugin)
{
    public async Task CreateMissingCodes()
    {
        var c = await db.ServiceRequests
            .AsNoTracking()
            .Where(n => n.Description.BodySite == null)
            .CountAsync();
        logger.LogInformation("{c} requests without body site coding", c);


        // prepare plugin 
        var sr = await db.ServiceRequests
            .AsNoTracking()
           .Where(n => n.Description.BodySite == null && n.Description.Text != null && n.Description.Text.Contains("breast"))
           .OrderByDescending(n => n.CreatedOn)
           .FirstOrDefaultAsync();

        logger.LogInformation("processing requests {srid} with description {desc}", sr.Id, sr.Description?.Text);

        // create the kernel
        var kernelBuilder = Kernel.CreateBuilder();
        var ollamaHttpClient = new HttpClient
        {
            BaseAddress = new Uri("http://localhost:11434"),
            Timeout = TimeSpan.FromMinutes(10)
        };
        kernelBuilder.AddOllamaChatCompletion("llama3.2:latest", ollamaHttpClient);
        kernelBuilder.Plugins.AddFromObject(plugin);
        var kernel = kernelBuilder.Build();

        var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();
        var settings = new OllamaPromptExecutionSettings { FunctionChoiceBehavior = FunctionChoiceBehavior.Auto() };

        var history = new ChatHistory();
        history.AddUserMessage($"""
            Load the Request with id = {sr.Id} 
            Then get the description of the resource and analyze if you can determine the described body site 
            from the given icd-o concepts list - each concept has a Display text for comparison. Typically, the body site is prefixed with site: 
            the code must be in the list that you get over the get_bodysites function.
            guess the probability that your choice is correct and then set the body site code and it probability. 
            Finally tell me, what concept.code you have chosen.
"""
);

        logger.LogInformation("thinking ...");
        ChatMessageContent chatResult = await chatCompletionService.GetChatMessageContentAsync(history, settings, kernel);
        Console.WriteLine(chatResult.Content);
    }
}
