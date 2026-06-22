using System.Text.Json;
using iPath.Application.AI;
using iPath.Application.Coding;
using iPath.Domain.Entities;
using iPath.EF.Core.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace iPath.Database.EFCore.AI;

public class AiExtractionService : IAiExtractionService
{
    private readonly IChatClient _chatClient;
    private readonly IPromptContextResolver _promptResolver;
    private readonly iPathDbContext _dbContext;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AiExtractionService> _logger;

    public AiExtractionService(
        IChatClient chatClient,
        IPromptContextResolver promptResolver,
        iPathDbContext dbContext,
        IServiceProvider serviceProvider,
        ILogger<AiExtractionService> logger)
    {
        _chatClient = chatClient;
        _promptResolver = promptResolver;
        _dbContext = dbContext;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task<AiExtractionResult> ExtractAsync(string rawText, Guid? communityId, Guid? groupId, Guid? userId, CancellationToken ct = default)
    {
        var resolvedConfig = await _promptResolver.ResolveConfigAsync(communityId, groupId, userId);

        var systemPrompt = resolvedConfig.SystemInstructionsOverride ?? _promptResolver.GetDefaultSystemPrompt();

        string modelUsed = resolvedConfig.PreferredModelId ?? "llama3";

        var messages = new List<ChatMessage>
        {
            new ChatMessage(ChatRole.System, systemPrompt),
            new ChatMessage(ChatRole.User, rawText)
        };

        var options = new ChatOptions
        {
            ResponseFormat = ChatResponseFormat.Json
        };

        _logger.LogInformation("Sending extraction request to AI model {Model}", modelUsed);
        var chatResponse = await _chatClient.GetResponseAsync(messages, options, ct);
        
        if (!string.IsNullOrEmpty(chatResponse.ModelId))
        {
            modelUsed = chatResponse.ModelId;
        }

        string responseText = chatResponse.Text ?? string.Empty;
        _logger.LogDebug("AI response received: {Response}", responseText);

        ExtractedPayload? payload = null;
        try
        {
            payload = JsonSerializer.Deserialize<ExtractedPayload>(responseText, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deserialize AI response: {Response}", responseText);
        }

        payload ??= new ExtractedPayload();

        string valueSetId = "icdo-topo";
        if (communityId.HasValue)
        {
            var community = await _dbContext.Communities
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == communityId.Value, ct);

            if (!string.IsNullOrEmpty(community?.Settings?.TopographyValueSet))
            {
                valueSetId = community.Settings.TopographyValueSet;
            }
        }
        else if (groupId.HasValue)
        {
            var group = await _dbContext.Groups
                .AsNoTracking()
                .FirstOrDefaultAsync(g => g.Id == groupId.Value, ct);

            if (group?.CommunityId != null)
            {
                var community = await _dbContext.Communities
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Id == group.CommunityId.Value, ct);

                if (!string.IsNullOrEmpty(community?.Settings?.TopographyValueSet))
                {
                    valueSetId = community.Settings.TopographyValueSet;
                }
            }
        }

        bool isTopographyValid = false;
        string? topographyCode = payload.TopographyCode?.Trim();
        string? topographyName = payload.TopographyName;

        if (!string.IsNullOrWhiteSpace(topographyCode))
        {
            try
            {
                var codingService = _serviceProvider.GetKeyedService<CodingService>("icdo");
                if (codingService != null)
                {
                    await codingService.LoadCodeSystem();
                    await codingService.LoadValueSet(valueSetId);

                    var vsDisplay = codingService.GetValueSetDisplay(valueSetId);
                    var codeDisplay = vsDisplay?.GetByCode(topographyCode);
                    if (codeDisplay != null && codeDisplay.InValueSet)
                    {
                        isTopographyValid = true;
                        if (!string.IsNullOrWhiteSpace(codeDisplay.Display))
                        {
                            topographyName = codeDisplay.Display;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Topography validation failed during code lookup for {Code} in ValueSet {ValueSet}", topographyCode, valueSetId);
            }
        }

        return new AiExtractionResult(
            Age: payload.Age,
            Sex: NormalizeSex(payload.Sex),
            TopographyCode: topographyCode,
            TopographyName: topographyName,
            ClinicalQuestions: payload.ClinicalQuestions ?? new List<string>(),
            Snippet: payload.Snippet,
            ModelUsed: modelUsed,
            RawSuggestedJson: responseText,
            IsTopographyValid: isTopographyValid
        );
    }

    public async Task SaveCorrectionDeltaAsync(Guid? groupId, string fieldName, string? wrongPrediction, string? correctedValue, string? snippet, CancellationToken ct = default)
    {
        var delta = new AiCorrectionDelta
        {
            GroupId = groupId,
            FieldName = fieldName,
            WrongPrediction = wrongPrediction,
            CorrectedValue = correctedValue,
            ContextualSnippet = snippet
        };

        await _dbContext.AiCorrectionDeltas.AddAsync(delta, ct);
        await _dbContext.SaveChangesAsync(ct);
    }

    public async Task SaveIngestionLineageAsync(Guid caseId, Guid? groupId, string rawText, string aiSuggestedJson, string humanAcceptedJson, string modelUsed, bool wasOverridden, CancellationToken ct = default, string status = "Completed")
    {
        var lineage = new CaseIngestionLineage
        {
            CaseId = caseId,
            GroupId = groupId,
            RawInputText = rawText,
            AiSuggestedDataJson = aiSuggestedJson,
            HumanAcceptedDataJson = humanAcceptedJson,
            ModelIdentifierUsed = modelUsed,
            WasOverridden = wasOverridden,
            Status = status
        };

        await _dbContext.CaseIngestionLineages.AddAsync(lineage, ct);
        await _dbContext.SaveChangesAsync(ct);
    }

    public static bool ApplyExtractionToCase(ServiceRequest caseItem, AiExtractionResult result, CodingService codingService)
    {
        if (caseItem.Description == null) return false;

        bool caseUpdated = false;

        caseItem.Description.PatientInfo ??= new PatientInfo();

        if (!caseItem.Description.PatientInfo.Age.HasValue && result.Age.HasValue)
        {
            caseItem.Description.PatientInfo.Age = result.Age;
            caseUpdated = true;
        }

        if (string.IsNullOrEmpty(caseItem.Description.PatientInfo.Gender) && !string.IsNullOrEmpty(result.Sex) && result.Sex != "U")
        {
            caseItem.Description.PatientInfo.Gender = result.Sex;
            caseUpdated = true;
        }

        if (caseItem.Description.BodySite == null && !string.IsNullOrEmpty(result.TopographyCode) && result.IsTopographyValid)
        {
            caseItem.Description.BodySite = new CodedConcept
            {
                Code = result.TopographyCode,
                Display = result.TopographyName ?? result.TopographyCode,
                System = codingService.CodeSystemUrl
            };
            caseUpdated = true;
        }

        return caseUpdated;
    }

    private static string NormalizeSex(string? sex)
    {
        if (string.IsNullOrWhiteSpace(sex)) return "U";
        var s = sex.Trim().ToUpperInvariant();
        if (s.StartsWith("M")) return "M";
        if (s.StartsWith("F") || s.StartsWith("W")) return "F";
        return "U";
    }

    private class ExtractedPayload
    {
        public int? Age { get; set; }
        public string? Sex { get; set; }
        public string? TopographyCode { get; set; }
        public string? TopographyName { get; set; }
        public List<string>? ClinicalQuestions { get; set; }
        public string? Snippet { get; set; }
    }
}
