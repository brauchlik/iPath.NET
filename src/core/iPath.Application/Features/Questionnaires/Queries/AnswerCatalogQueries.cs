namespace iPath.Application.Features.Questionnaires.Queries;

/// <summary>
/// One concept of the catalog: a question the group's case description forms can record, merged
/// across forms by its code (which is what makes the same question in two forms one entry), with
/// what the data says about it.
/// </summary>
public record CatalogConceptDto(
    string Key,
    string? CodeSystem,
    string? Code,
    string Display,
    bool HasCode,
    string? ValueType,
    bool Repeats,
    IReadOnlyList<string> Options,
    IReadOnlyList<string> LinkIds,
    IReadOnlyList<string> Forms,
    int AnsweredCases,
    int AnsweredRows);

/// <summary>A case description form: assigned to the group and/or found in its cases' answers.</summary>
public record CatalogFormDto(
    string QuestionnaireId,
    string Name,
    int Version,
    bool Assigned,
    int CasesWithAnswers,
    IReadOnlyList<ConformityFinding> Findings);

public record AnswerCatalogDto(
    IReadOnlyList<CatalogFormDto> Forms,
    IReadOnlyList<CatalogConceptDto> Concepts,
    IReadOnlyList<string> Warnings,
    int CasesInGroup);

/// <summary>
/// The catalog of one group: which case description forms it has, which of them its cases use, and
/// the concepts those forms can record - built from the forms' active definitions and annotated with
/// what the cases actually answered.
/// </summary>
public record GetAnswerCatalogQuery(Guid GroupId) : IRequest<GetAnswerCatalogQuery, Task<AnswerCatalogDto>>;
