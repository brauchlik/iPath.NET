using iPath.Application.AI;
using iPath.Application.Features.Admin;
using iPath.Domain.Config;
using iPath.EF.Core.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace iPath.EF.Core.FeatureHandlers.Admin;

public class GetAiStatusHandler(
    iPathDbContext db,
    IOptions<AiSettingsConfig> aiOpts,
    IConfiguration config,
    IAiExtractionQueue queue,
    IChatClient chatClient,
    ILogger<GetAiStatusHandler> logger)
    : IRequestHandler<GetAiStatusQuery, Task<AiStatusDto>>
{
    public async Task<AiStatusDto> Handle(GetAiStatusQuery request, CancellationToken ct)
    {
        var isEnabled = aiOpts.Value.IsEnabled;
        var provider = aiOpts.Value.Provider;
        
        var dto = new AiStatusDto
        {
            IsEnabled = isEnabled,
            Provider = provider,
            QueueLength = queue.GetQueueCount()
        };

        if (isEnabled)
        {
            var aiSection = config.GetSection(AiSettingsConfig.ConfigName);

            // Set models
            dto.ChatModel = aiSection.GetValue<string>($"{provider}:ChatModel") ?? "";
            dto.TranslationModel = aiSection.GetValue<string>($"{provider}:TranslationModel") ?? dto.ChatModel;
            dto.EmbeddingModel = aiSection.GetValue<string>($"{provider}:EmbeddingModel") ?? "";

            if (request.CheckConnection)
            {
                // Check if LLM is online
                try
                {
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    cts.CancelAfter(TimeSpan.FromSeconds(5));

                    if (provider.Equals("Ollama", StringComparison.OrdinalIgnoreCase))
                    {
                        var baseUriStr = aiSection.GetValue<string>("Ollama:BaseUri") ?? "http://localhost:11434/";
                        using var httpClient = new System.Net.Http.HttpClient();
                        var response = await httpClient.GetAsync(baseUriStr, cts.Token);
                        dto.IsLlmOnline = response.IsSuccessStatusCode;
                    }
                    else
                    {
                        var messages = new List<ChatMessage> { new ChatMessage(ChatRole.User, "ping") };
                        var options = new ChatOptions { MaxOutputTokens = 5 };
                        var response = await chatClient.GetResponseAsync(messages, options, cts.Token);
                        dto.IsLlmOnline = response != null;
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "AI health check failed: {Message}", ex.Message);
                    dto.IsLlmOnline = false;
                }
            }
        }

        // Fetch recent activities
        try
        {
            var activities = await db.CaseIngestionLineages
                .AsNoTracking()
                .OrderByDescending(l => l.Timestamp)
                .Take(10)
                .Join(db.ServiceRequests,
                    lineage => lineage.CaseId,
                    caseItem => caseItem.Id,
                    (lineage, caseItem) => new AiActivityDto
                    {
                        Id = lineage.Id,
                        CaseId = lineage.CaseId,
                        CaseTitle = caseItem.Description != null ? (caseItem.Description.Title ?? "Untitled Case") : "Untitled Case",
                        ModelUsed = lineage.ModelIdentifierUsed ?? "Unknown",
                        WasOverridden = lineage.WasOverridden,
                        Timestamp = lineage.Timestamp,
                        Status = lineage.Status,
                        ErrorMessage = lineage.ErrorMessage
                    })
                .ToListAsync(ct);

            dto.RecentActivities = activities;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error fetching recent AI activities");
        }

        return dto;
    }
}
