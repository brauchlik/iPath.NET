using iPath.Domain.Entities;

namespace iPath.Application.AI;

public record AiExtractionResult(
    int? Age,
    string? Sex, // M, F, U
    string? TopographyCode,
    string? TopographyName,
    List<string> ClinicalQuestions,
    string? Snippet,
    string ModelUsed,
    string RawSuggestedJson,
    bool IsTopographyValid
);

public interface IAiExtractionService
{
    Task<AiExtractionResult> ExtractAsync(string rawText, Guid? communityId, Guid? groupId, Guid? userId, CancellationToken ct = default);
    Task SaveCorrectionDeltaAsync(Guid? groupId, string fieldName, string? wrongPrediction, string? correctedValue, string? snippet, CancellationToken ct = default);
    Task SaveIngestionLineageAsync(Guid caseId, Guid? groupId, string rawText, string aiSuggestedJson, string humanAcceptedJson, string modelUsed, bool wasOverridden, CancellationToken ct = default, string status = "Completed");
}
