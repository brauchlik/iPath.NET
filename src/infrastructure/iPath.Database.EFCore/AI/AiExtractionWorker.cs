using iPath.Application.AI;
using iPath.Application.Coding;
using iPath.Domain.Entities;
using iPath.EF.Core.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace iPath.Database.EFCore.AI;

public class AiExtractionWorker : BackgroundService
{
    private readonly IAiExtractionQueue _queue;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AiExtractionWorker> _logger;

    public AiExtractionWorker(
        IAiExtractionQueue queue,
        IServiceProvider serviceProvider,
        ILogger<AiExtractionWorker> logger)
    {
        _queue = queue;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AiExtractionWorker background service has started.");

        var concreteQueue = _queue as AiExtractionQueue;

        await foreach (var caseId in _queue.ReadAllAsync(stoppingToken))
        {
            _logger.LogInformation("Processing CaseId {CaseId} from extraction queue", caseId);

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<iPathDbContext>();
                var promptResolver = scope.ServiceProvider.GetRequiredService<IPromptContextResolver>();

                var caseItem = await db.ServiceRequests
                    .Include(c => c.Group)
                    .FirstOrDefaultAsync(c => c.Id == caseId, stoppingToken);

                if (caseItem?.Description == null) continue;

                var extractionText = caseItem.Description.GetExtractionText();
                if (string.IsNullOrWhiteSpace(extractionText))
                {
                    _logger.LogWarning("CaseId {CaseId} has no extraction text", caseId);
                    continue;
                }

                var aiConfig = await promptResolver.ResolveConfigAsync(caseItem.Group?.CommunityId, caseItem.GroupId, caseItem.OwnerId);

                // Create lineage record immediately on dequeue so table shows "Processing"
                var lineage = new CaseIngestionLineage
                {
                    CaseId = caseItem.Id,
                    GroupId = caseItem.GroupId,
                    RawInputText = extractionText,
                    Status = "Processing",
                    ModelIdentifierUsed = aiConfig.IsEnabled ? null : "none",
                    AiSuggestedDataJson = aiConfig.IsEnabled ? "" : "{}"
                };
                db.CaseIngestionLineages.Add(lineage);
                await db.SaveChangesAsync(stoppingToken);

                try
                {
                    if (aiConfig.IsEnabled)
                    {
                        _logger.LogInformation("Running AI extraction for CaseId {CaseId}", caseItem.Id);

                        var extractionService = scope.ServiceProvider.GetRequiredService<IAiExtractionService>();
                        var semanticSearchService = scope.ServiceProvider.GetRequiredService<ISemanticSearchService>();

                        if (!await db.CaseEmbeddings.AnyAsync(e => e.CaseId == caseItem.Id, stoppingToken))
                        {
                            await semanticSearchService.SaveEmbeddingAsync(caseItem.Id, extractionText, stoppingToken);
                        }

                        var result = await extractionService.ExtractAsync(
                            extractionText,
                            caseItem.Group?.CommunityId,
                            caseItem.GroupId,
                            caseItem.OwnerId,
                            stoppingToken
                        );

                        var codingService = scope.ServiceProvider.GetRequiredKeyedService<CodingService>("icdo");
                        bool caseUpdated = AiExtractionService.ApplyExtractionToCase(caseItem, result, codingService);

                        if (caseUpdated)
                        {
                            db.ServiceRequests.Update(caseItem);
                        }

                        lineage.AiSuggestedDataJson = result.RawSuggestedJson;
                        lineage.ModelIdentifierUsed = result.ModelUsed;
                        lineage.Status = "Completed";
                    }
                    else
                    {
                        lineage.Status = "Skipped";
                    }
                }
                catch (Exception ex)
                {
                    lineage.Status = "Failed";
                    lineage.ErrorMessage = $"{ex.GetType().Name}: {ex.Message}";
                    _logger.LogError(ex, "AI extraction failed for CaseId {CaseId}", caseItem.Id);
                }

                await db.SaveChangesAsync(stoppingToken);
                _logger.LogInformation("Completed AI extraction for CaseId {CaseId} with status {Status}", caseItem.Id, lineage.Status);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while setting up AI extraction for CaseId {CaseId}", caseId);
            }
            finally
            {
                concreteQueue?.Dequeue(caseId);
            }
        }
    }
}
