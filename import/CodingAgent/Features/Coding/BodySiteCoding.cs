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

        logger.LogInformation("processing requests {srid}", sr.Id);

        // create the kernel
        var kernelBuilder = Kernel.CreateBuilder();
        var ollamaHttpClient = new HttpClient
        {
            BaseAddress = new Uri("http://localhost:11434"),
            Timeout = TimeSpan.FromMinutes(10)
        };
        kernelBuilder.AddOllamaChatCompletion("mistral:7b", ollamaHttpClient);
        kernelBuilder.Plugins.AddFromObject(plugin);
        var kernel = kernelBuilder.Build();

        var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();
        var settings = new OllamaPromptExecutionSettings { FunctionChoiceBehavior = FunctionChoiceBehavior.Auto() };

        var history = new ChatHistory();
        history.AddUserMessage($"""
You are a cancer registry agent. Your task is to classify the body site from which the specimen for a pathology request has been taken.

**Instructions:**
- Use ONLY the functions provided by the CodingPlugin for all data access and actions.
- First, call `load_request` with id = {sr.Id} to load the request and get its description.
- Next, call `get_bodysites` to retrieve the list of valid ICD-O topography codes. **You must select a code ONLY from this list.**
- Analyze the description and select the most appropriate ICD-O code from the list.
- Estimate the probability that your choice is correct.
- Finally, call `set_bodysitecode` with the selected code and probability to save your result. If you cannot determine a code, set the code to "--".
- Do NOT invent codes or skip the save step.

**Remember:** All codes must come from the list returned by `get_bodysites`, and you must use `set_bodysitecode` to save your answer.
""");
        //        history.AddUserMessage($"""
        //            You are a cancer registry agent. Your task is to classify the body site from which the specimen for a pathology request has been taken.
        //            Load the Request with id = {sr.Id} from the database and read the description of the resource and analyze if you can determine the described body site. 
        //            Please use only the codes from the given ICD-O concepts list - each concept has a Display text for comparison. Do not use ICD-10, but ICD-O for oncology only.            
        //            guess the probability that your choice is correct and then set the body site code and it probability. 
        //            Finally use the set_bodysitecode function in the plugin to save the code. if it's not possible to determine a topography from the text, set the code to "--"
        //"""
        //);

        logger.LogInformation("thinking ...");
        ChatMessageContent chatResult = await chatCompletionService.GetChatMessageContentAsync(history, settings, kernel);
        Console.WriteLine(chatResult.Content);
    }
}
