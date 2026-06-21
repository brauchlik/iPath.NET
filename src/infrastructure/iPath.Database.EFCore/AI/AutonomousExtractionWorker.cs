using iPath.Application.AI;
using iPath.Application.Coding;
using iPath.Domain.Entities;
using iPath.EF.Core.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace iPath.Database.EFCore.AI;

public class AutonomousExtractionWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AutonomousExtractionWorker> _logger;

    public AutonomousExtractionWorker(
        IServiceProvider serviceProvider,
        ILogger<AutonomousExtractionWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AutonomousExtractionWorker Background Service has started.");

        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingCasesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred in AutonomousExtractionWorker processing loop");
            }

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }

    private async Task ProcessPendingCasesAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<iPathDbContext>();
        var promptResolver = scope.ServiceProvider.GetRequiredService<IPromptContextResolver>();
        var extractionService = scope.ServiceProvider.GetRequiredService<IAiExtractionService>();
        var semanticSearchService = scope.ServiceProvider.GetRequiredService<ISemanticSearchService>();

        var pendingCases = await db.ServiceRequests
            .Include(c => c.Group)
            .Where(c => !db.CaseIngestionLineages.Any(l => l.CaseId == c.Id))
            .OrderBy(c => c.ipath2_id.HasValue)
            .Take(10)
            .ToListAsync(ct);

        if (pendingCases.Count == 0) return;

        _logger.LogInformation("Found {Count} pending cases for autonomous AI processing", pendingCases.Count);

        foreach (var caseItem in pendingCases)
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                var aiConfig = await promptResolver.ResolveConfigAsync(caseItem.Group?.CommunityId, caseItem.GroupId, caseItem.OwnerId);
                
                if (caseItem.Description != null && !string.IsNullOrWhiteSpace(caseItem.Description.Text))
                {
                    if (!await db.CaseEmbeddings.AnyAsync(e => e.CaseId == caseItem.Id, ct))
                    {
                        await semanticSearchService.SaveEmbeddingAsync(caseItem.Id, caseItem.Description.Text, ct);
                    }

                    if (aiConfig.IsEnabled)
                    {
                        _logger.LogInformation("Processing autonomous entity extraction for CaseId {CaseId}", caseItem.Id);
                        
                        var result = await extractionService.ExtractAsync(
                            caseItem.Description.Text,
                            caseItem.Group?.CommunityId,
                            caseItem.GroupId,
                            caseItem.OwnerId,
                            ct
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
                        await db.CaseIngestionLineages.AddAsync(lineage, ct);
                    }
                    else
                    {
                        var lineage = new CaseIngestionLineage
                        {
                            CaseId = caseItem.Id,
                            GroupId = caseItem.GroupId,
                            RawInputText = caseItem.Description.Text,
                            AiSuggestedDataJson = "{}",
                            HumanAcceptedDataJson = null,
                            ModelIdentifierUsed = "none",
                            WasOverridden = false
                        };
                        await db.CaseIngestionLineages.AddAsync(lineage, ct);
                    }

                    await db.SaveChangesAsync(ct);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to autonomously process CaseId {CaseId}", caseItem.Id);
            }
        }
    }
}
