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


/// <summary>
/// One column of the answer pivot: the concept as it appears in the data. Key is the column identity
/// (system|code), Display is what the header shows - the item's label, or the linkId for uncoded
/// items, which are marked by HasCode being false.
/// </summary>
public record AnswerColumnDto(string Key, string? CodeSystem, string? Code, string? Display, bool HasCode);

/// <summary>One case per row. Cells are keyed by <see cref="AnswerColumnDto.Key"/> and already rendered.</summary>
public record AnswerCaseRowDto(
    Guid ServiceRequestId,
    string? Title,
    DateTime? CreatedOn,
    string? AccessionNo,
    string? BodySite,
    Dictionary<string, string?> Cells);

public record AnswerTableDto(IReadOnlyList<AnswerColumnDto> Columns, PagedResultList<AnswerCaseRowDto> Rows,
    int ConceptsOutsideColumns = 0);

/// <summary>Pivot of the extracted answers: cases as rows, concepts as columns.</summary>
public class GetAnswerTableQuery : PagedQuery<AnswerCaseRowDto>
    , IRequest<GetAnswerTableQuery, Task<AnswerTableDto>>
{
    public Guid? GroupId { get; set; }
    public string? QuestionnaireId { get; set; }
    public string? SearchString { get; set; }

    /// <summary>
    /// Columns to render, in this order - the selection made from the catalog. Null or empty means
    /// "columns from the answers", i.e. only the concepts the cohort actually answered. Sent columns
    /// stay visible even when no case answered them, which is what makes a selection comparable.
    /// </summary>
    public AnswerColumnDto[]? Columns { get; set; }
}
