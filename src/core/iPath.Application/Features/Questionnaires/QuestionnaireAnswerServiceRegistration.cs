using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace iPath.Application.Features.Questionnaires;

public static class QuestionnaireAnswerServiceRegistration
{
    // Called from AddIPathAPI (server host and tests) and from AddRazorLibServices (server and WASM
    // client): the extractor is stateless and shared, and the admin pages reach the checker through
    // the API, so both hosts need the same registrations. TryAdd keeps a second call a no-op.
    public static IServiceCollection AddQuestionnaireAnswerServices(this IServiceCollection services)
    {
        services.TryAddSingleton<IQuestionnaireAnswerExtractor, QuestionnaireAnswerExtractor>();
        services.TryAddSingleton<IQuestionnaireConformityChecker, QuestionnaireConformityChecker>();
        return services;
    }
}
