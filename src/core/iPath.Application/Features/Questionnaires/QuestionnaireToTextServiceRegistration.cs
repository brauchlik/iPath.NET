using iPath.Domain.Config;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace iPath.Application.Features.Questionnaires;

public static class QuestionnaireToTextServiceRegistration
{
    // Called from AddIPathAPI (server host and tests) and from AddRazorLibServices (server and WASM
    // client), because the services are consumed in both: UpdateServiceRequestHandler persists
    // GeneratedText server-side, and the admin page renders the preview in whichever host it runs.
    // TryAdd keeps the second call a no-op, so this list stays the single source for the keys.
    public static IServiceCollection AddQuestionnaireToTextServices(this IServiceCollection services)
    {
        services.TryAddKeyedTransient<IQuestionnaireToTextService, GenericQuestionnaireToListTextService>("Default List");
        services.TryAddKeyedTransient<IQuestionnaireToTextService, GenericQuestionnaireToCvsTextService>("CSV");
        services.TryAddKeyedTransient<IQuestionnaireToTextService>("Case Description (Compact)",
            (_, _) => new CaseDescriptionToTextService(CaseDescriptionOutputMode.Compact));
        services.TryAddKeyedTransient<IQuestionnaireToTextService>("Case Description (Expanded)",
            (_, _) => new CaseDescriptionToTextService(CaseDescriptionOutputMode.Expanded));

        // the default mode is configuration driven; iPathClientConfig is bound on the server from
        // appsettings and on the WASM client from api/v1/config, so both hosts agree
        services.TryAddSingleton<IQuestionnaireToTextServiceRegistry>(sp =>
            new QuestionnaireToTextServiceRegistry(
                sp.GetService<IOptions<iPathClientConfig>>()?.Value.DefaultTextPreviewService));

        return services;
    }
}
