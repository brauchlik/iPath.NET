using Hl7.Fhir.Model;

namespace iPath.Application.Features.Questionnaires;

public interface IQuestionnaireToTextService
{
    string CreateText(QuestionnaireResponse response, Questionnaire questionnaire);
}
