using iPath.Application.AI;
using iPath.Application.Coding;
using iPath.Domain.Entities;
using iPath.EF.Core.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace iPath.Database.EFCore.AI;

public class AiTranscriptionQueueWorker : BackgroundService
{
    private readonly IAiTranscriptionQueue _queue;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AiTranscriptionQueueWorker> _logger;

    public AiTranscriptionQueueWorker(
        IAiTranscriptionQueue queue,
        IServiceProvider serviceProvider,
        ILogger<AiTranscriptionQueueWorker> logger)
    {
        _queue = queue;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AiTranscriptionQueueWorker background service has started.");

        var concreteQueue = _queue as AiTranscriptionQueue;

        await foreach (var caseId in _queue.ReadAllAsync(stoppingToken))
        {
            _logger.LogInformation("Processing CaseId {CaseId} from transcription queue", caseId);

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<iPathDbContext>();
                var promptResolver = scope.ServiceProvider.GetRequiredService<IPromptContextResolver>();
                var extractionService = scope.ServiceProvider.GetRequiredService<IAiExtractionService>();
                var semanticSearchService = scope.ServiceProvider.GetRequiredService<ISemanticSearchService>();

                var caseItem = await db.ServiceRequests
                    .Include(c => c.Group)
                    .FirstOrDefaultAsync(c => c.Id == caseId, stoppingToken);

                if (caseItem != null && caseItem.Description != null && !string.IsNullOrWhiteSpace(caseItem.Description.Text))
                {
                    // 1. Generate & Save Embedding
                    _logger.LogInformation("Generating and saving vector embedding for CaseId {CaseId}", caseId);
                    await semanticSearchService.SaveEmbeddingAsync(caseItem.Id, caseItem.Description.Text, stoppingToken);

                    // 2. Perform Extraction
                    _logger.LogInformation("Calling AI Extraction Service for CaseId {CaseId}", caseId);
                    var result = await extractionService.ExtractAsync(
                        caseItem.Description.Text,
                        caseItem.Group?.CommunityId,
                        caseItem.GroupId,
                        caseItem.OwnerId,
                        stoppingToken
                    );

                    bool caseUpdated = false;

                    if (caseItem.Description.PatientInfo == null)
                    {
                        caseItem.Description.PatientInfo = new PatientInfo();
                    }

                    if (!caseItem.Description.PatientInfo.Age.HasValue && result.Age.HasValue)
                    {
                        caseItem.Description.PatientInfo.Age = result.Age;
                        caseUpdated = true;
                    }

                    if (string.IsNullOrEmpty(caseItem.Description.PatientInfo.Gender) && !string.IsNullOrEmpty(result.Sex))
                    {
                        caseItem.Description.PatientInfo.Gender = result.Sex;
                        caseUpdated = true;
                    }

                    if (caseItem.Description.BodySite == null && !string.IsNullOrEmpty(result.TopographyCode) && result.IsTopographyValid)
                    {
                        var codingService = scope.ServiceProvider.GetRequiredKeyedService<CodingService>("icdo");
                        caseItem.Description.BodySite = new CodedConcept
                        {
                            Code = result.TopographyCode,
                            Display = result.TopographyName ?? result.TopographyCode,
                            System = codingService.CodeSystemUrl
                        };
                        caseUpdated = true;
                    }

                    if (caseUpdated)
                    {
                        db.ServiceRequests.Update(caseItem);
                    }

                    // Save Lineage Log
                    var lineage = new CaseIngestionLineage
                    {
                        CaseId = caseItem.Id,
                        GroupId = caseItem.GroupId,
                        RawInputText = caseItem.Description.Text,
                        AiSuggestedDataJson = result.RawSuggestedJson,
                        HumanAcceptedDataJson = null,
                        ModelIdentifierUsed = result.ModelUsed,
                        WasOverridden = false
                    };
                    await db.CaseIngestionLineages.AddAsync(lineage, stoppingToken);

                    await db.SaveChangesAsync(stoppingToken);
                    _logger.LogInformation("Successfully completed AI transcription for CaseId {CaseId}", caseId);
                }
                else
                {
                    _logger.LogWarning("CaseId {CaseId} was not found or contains empty clinical history text", caseId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while executing AI transcription for CaseId {CaseId}", caseId);
            }
            finally
            {
                concreteQueue?.Dequeue(caseId);
            }
        }
    }
}
