using iPath.Application.Coding;
using iPath.Application.Features;
using iPath.Blazor.Componenents.Questionaires;
using iPath.Domain.Entities;

namespace iPath.Test.xUnit2.Questionnaires;

// Mirrors the exact call the case-creation wizard makes:
// ServiceRequestCreateWizzardViewModel.cs — ActiveGroup.Questionnaires.OrderBy(f => f.Priority).FilterAsync(usage, bodySiteCode)
[Collection("icdo")]
public class QuestionnaireBodySiteFilterTests : IClassFixture<iPathFixture>
{
    private readonly CodingService _coding;

    public QuestionnaireBodySiteFilterTests(iPathFixture fixture)
    {
        _coding = new CodingService(fixture.ServiceProvider, "icdo");
        QuestionnaireExtension.Initialize(_coding);
    }

    private static QuestionnaireForGroupDto Questionnaire(string name, eQuestionnaireUsage usage, int priority, string? bodySiteCode = null) =>
        new(Guid.NewGuid(), name, name, usage, new QuestionnaireSettings
        {
            BodySiteFilter = bodySiteCode is null ? null : new ConceptFilter
            {
                Concetps = [new CodedConcept { System = CodedConcept.IcodUrl, Code = bodySiteCode, Display = bodySiteCode }]
            }
        }, priority);

    [Fact]
    public async Task NoFilter_MatchesAnyBodySite()
    {
        var forms = new[] { Questionnaire("General", eQuestionnaireUsage.CaseDescription, 1) };

        var result = await forms.FilterAsync(eQuestionnaireUsage.CaseDescription, "C61");

        result.Should().ContainSingle().Which.QuestinnaireName.Should().Be("General");
    }

    [Fact]
    public async Task FilterOnParentCode_MatchesMoreSpecificChildCode()
    {
        // C50 = breast (general), C50.1 = upper-outer quadrant of breast
        var forms = new[] { Questionnaire("Breast", eQuestionnaireUsage.CaseDescription, 1, "C50") };

        var result = await forms.FilterAsync(eQuestionnaireUsage.CaseDescription, "C50.1");

        result.Should().ContainSingle().Which.QuestinnaireName.Should().Be("Breast");
    }

    [Fact]
    public async Task FilterOnChildCode_DoesNotMatchBroaderParentCode()
    {
        var forms = new[] { Questionnaire("Breast upper-outer only", eQuestionnaireUsage.CaseDescription, 1, "C50.1") };

        var result = await forms.FilterAsync(eQuestionnaireUsage.CaseDescription, "C50");

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task FilterOnUnrelatedBranch_DoesNotMatch()
    {
        var forms = new[] { Questionnaire("Breast", eQuestionnaireUsage.CaseDescription, 1, "C50") };

        var result = await forms.FilterAsync(eQuestionnaireUsage.CaseDescription, "C61"); // prostate

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task WrongUsage_IsExcludedEvenWhenBodySiteMatches()
    {
        var forms = new[] { Questionnaire("Comment form", eQuestionnaireUsage.Annotation, 1, "C50") };

        var result = await forms.FilterAsync(eQuestionnaireUsage.CaseDescription, "C50");

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task MultipleMatches_AreAllReturned_LowestPriorityFirst()
    {
        var forms = new[]
        {
            Questionnaire("Specific breast form", eQuestionnaireUsage.CaseDescription, priority: 2, bodySiteCode: "C50.1"),
            Questionnaire("General breast form", eQuestionnaireUsage.CaseDescription, priority: 1, bodySiteCode: "C50"),
        };

        var result = (await forms.OrderBy(f => f.Priority).FilterAsync(eQuestionnaireUsage.CaseDescription, "C50.1")).ToList();

        result.Should().HaveCount(2);
        result.First().QuestinnaireName.Should().Be("General breast form");
    }
}
