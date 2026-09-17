namespace iPath.Application.Features.Questionnaires.Queries;

public record ServiceRequestAnswerDto(
    Guid Id,
    Guid ServiceRequestId,
    string? CaseTitle,
    DateTime? CaseCreatedOn,
    string QuestionnaireId,
    int? QuestionnaireVersion,
    string LinkId,
    string? CodeSystem,
    string? Code,
    string? CodeDisplay,
    string? OtherCodings,
    string ValueType,
    string? Value,
    string? ValueDisplay,
    string? Unit,
    int ExtractionVersion);

/// <summary>Extraction state of one case, so "response present but nothing extracted" is visible.</summary>
public record AnswerExtractionStateDto(
    Guid ServiceRequestId,
    string? Title,
    bool HasResponse,
    string? QuestionnaireId,
    int? QuestionnaireVersion,
    DateTime? ExtractedOn,
    int? ExtractionVersion,
    int? AnswerCount,
    string? Error,
    bool ExtractionDisabled = false);

public record CaseAnswersDto(AnswerExtractionStateDto State, List<ServiceRequestAnswerDto> Answers);

public record GetAnswersByCaseQuery(Guid ServiceRequestId)
    : IRequest<GetAnswersByCaseQuery, Task<CaseAnswersDto>>;

public class GetAnswersByFilterQuery : PagedQuery<ServiceRequestAnswerDto>
    , IRequest<GetAnswersByFilterQuery, Task<PagedResultList<ServiceRequestAnswerDto>>>
{
    public Guid? GroupId { get; set; }
    public string? QuestionnaireId { get; set; }
    public string? SearchString { get; set; }
}

public record GetAnswerExtractionIssuesQuery(Guid? GroupId, int Max = 200)
    : IRequest<GetAnswerExtractionIssuesQuery, Task<List<AnswerExtractionStateDto>>>;
