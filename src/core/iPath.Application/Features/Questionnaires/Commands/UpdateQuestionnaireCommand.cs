namespace iPath.Application.Features;



public record UpdateQuestionnaireCommand(string QuestionnaireId, string Name, string Resource, QuestionnaireSettings Settings, bool IsActive, bool insert)
    : IRequest<UpdateQuestionnaireCommand, Task<Guid>>;