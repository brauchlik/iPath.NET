using Hl7.Fhir.Model;

namespace iPath.Application.Features.Questionnaires;

public record ConformityFinding(string Severity, string? LinkId, string Message);

public interface IQuestionnaireConformityChecker
{
    /// <summary>
    /// Reports the problems that would make answer extraction ambiguous or lossy. Uses the same
    /// index and key resolution as the extractor, so the two cannot drift apart.
    /// </summary>
    IReadOnlyList<ConformityFinding> Check(Questionnaire? questionnaire);
}

public class QuestionnaireConformityChecker : IQuestionnaireConformityChecker
{
    public IReadOnlyList<ConformityFinding> Check(Questionnaire? questionnaire)
    {
        var findings = new List<ConformityFinding>();
        if (questionnaire is null) return findings;

        var index = QuestionnaireItemIndex.Build(questionnaire);

        foreach (var linkId in index.DuplicateLinkIds)
        {
            findings.Add(new ConformityFinding("error", linkId,
                "linkId is used more than once - an extracted answer cannot be attributed to a single question"));
        }

        var seenCodes = new Dictionary<string, string>();
        foreach (var item in index.AllItems)
        {
            if (item.Type == Questionnaire.QuestionnaireItemType.Group) continue;

            var codings = item.Code?.Where(c => !string.IsNullOrEmpty(c.Code)).ToList() ?? [];
            if (codings.Count == 0)
            {
                var key = index.ResolveKey(item.LinkId);
                if (key.System == QuestionnaireItemIndex.DummySystem)
                {
                    findings.Add(new ConformityFinding("warning", item.LinkId,
                        $"no coding anywhere on the path - extracted under the generated key '{key.Code}'"));
                }
                else
                {
                    findings.Add(new ConformityFinding("info", item.LinkId,
                        $"no own coding - inherits '{key.System}|{key.Code}' from its parent"));
                }
            }

            foreach (var coding in codings)
            {
                var id = $"{coding.System}|{coding.Code}";
                if (seenCodes.TryGetValue(id, out var other) && other != item.LinkId)
                {
                    findings.Add(new ConformityFinding("warning", item.LinkId,
                        $"coding {id} is also used by '{other}' - both collapse into one extracted item"));
                }
                else
                {
                    seenCodes[id] = item.LinkId ?? string.Empty;
                }
            }

            if (item.Type == Questionnaire.QuestionnaireItemType.Reference)
            {
                findings.Add(new ConformityFinding("info", item.LinkId,
                    "answers of type reference are not extracted"));
            }
        }

        return findings;
    }
}
