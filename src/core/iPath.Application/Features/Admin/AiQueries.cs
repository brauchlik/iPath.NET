namespace iPath.Application.Features.Admin;

public record GetAiStatusQuery(bool CheckConnection = false)
    : IRequest<GetAiStatusQuery, Task<AiStatusDto>>;

public class AiStatusDto
{
    public bool IsEnabled { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string ChatModel { get; set; } = string.Empty;
    public string TranslationModel { get; set; } = string.Empty;
    public string EmbeddingModel { get; set; } = string.Empty;
    public bool? IsLlmOnline { get; set; }
    public int QueueLength { get; set; }
    public List<AiActivityDto> RecentActivities { get; set; } = new();
}

public class AiActivityDto
{
    public Guid Id { get; set; }
    public Guid CaseId { get; set; }
    public string CaseTitle { get; set; } = string.Empty;
    public string ModelUsed { get; set; } = string.Empty;
    public bool WasOverridden { get; set; }
    public DateTime Timestamp { get; set; }
    public string Status { get; set; } = "Queued";
    public string? ErrorMessage { get; set; }
}

public record GetAiLineageDetailQuery(Guid Id)
    : IRequest<GetAiLineageDetailQuery, Task<AiLineageDetailDto?>>;

public record GetAiLineageByCaseQuery(Guid CaseId)
    : IRequest<GetAiLineageByCaseQuery, Task<List<AiLineageDetailDto>>>;

public class AiLineageDetailDto
{
    public Guid Id { get; set; }
    public Guid CaseId { get; set; }
    public string? GroupId { get; set; }
    public string CaseTitle { get; set; } = string.Empty;
    public string RawInputText { get; set; } = string.Empty;
    public string AiSuggestedDataJson { get; set; } = string.Empty;
    public string? HumanAcceptedDataJson { get; set; }
    public string? ModelUsed { get; set; }
    public bool WasOverridden { get; set; }
    public DateTime Timestamp { get; set; }
    public string Status { get; set; } = "Queued";
    public string? ErrorMessage { get; set; }
    public string? Age { get; set; }
    public string? Sex { get; set; }
    public string? TopographyCode { get; set; }
    public string? TopographyName { get; set; }
}

public record AiEnqueueResult(bool Enqueued, string Message);
