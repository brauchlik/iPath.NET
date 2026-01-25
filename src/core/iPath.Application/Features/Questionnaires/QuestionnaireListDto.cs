namespace iPath.Application.Features;

public record QuestionnaireListDto (Guid Id, string QuestionnaireId, string Name, int Version, bool IsActive, string Filter);
