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
}
