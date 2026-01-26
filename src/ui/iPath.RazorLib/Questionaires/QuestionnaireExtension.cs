using Microsoft.Extensions.DependencyInjection;

namespace iPath.Blazor.Componenents.Questionaires;

public static class QuestionnaireExtension
{
    private static CodingService coding;

    public static void Initialize(IServiceProvider serviceProvider)
    {
        coding = serviceProvider.GetKeyedService<CodingService>("icdo");
    }

    extension(IEnumerable<QuestionnaireForGroupDto> list)
    {
        public async Task<IReadOnlyCollection<QuestionnaireForGroupDto>> FilterAsync(eQuestionnaireUsage qUsage, string? BodySiteCode)
        {
            await coding.LoadCodeSystem();

            var allForms = list.Where(q => q.Usage == qUsage);
            return allForms.Where(q => coding.InConceptFilter(BodySiteCode, q.Settings.BodySiteFilter)).ToList();
        }
    }
}