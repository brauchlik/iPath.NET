using Hl7.Fhir.Model;
using FhirCoding = Hl7.Fhir.Model.Coding;
using System.Globalization;

namespace iPath.Application.Features.Questionnaires;

/// <summary>One extracted answer, shaped like the Observation core: question identity, value, unit.</summary>
public record ExtractedAnswer(
    string LinkId,
    string? CodeSystem,
    string? Code,
    string? CodeDisplay,
    string? OtherCodings,
    string ValueType,
    string? Value,
    string? ValueDisplay,
    string? Unit);

public interface IQuestionnaireAnswerExtractor
{
    /// <summary>Incremented whenever the rules change, so stored rows can be recognised as stale.</summary>
    int Version { get; }

    /// <summary>
    /// Extracts only answered items. No answer means not observed - gate-false items and items the
    /// physician skipped are deliberately indistinguishable. Never throws on malformed input.
    /// </summary>
    IReadOnlyList<ExtractedAnswer> Extract(QuestionnaireResponse? response, Questionnaire? questionnaire);
}

public class QuestionnaireAnswerExtractor : IQuestionnaireAnswerExtractor
{
    public const int CurrentVersion = 1;
    private const string UnitOptionUrl = "http://hl7.org/fhir/StructureDefinition/questionnaire-unitOption";

    public int Version => CurrentVersion;

    public IReadOnlyList<ExtractedAnswer> Extract(QuestionnaireResponse? response, Questionnaire? questionnaire)
    {
        var result = new List<ExtractedAnswer>();
        if (response is null) return result;

        var index = QuestionnaireItemIndex.Build(questionnaire);
        Walk(index, response.Item, result);
        return result;
    }

    private void Walk(QuestionnaireItemIndex index, IEnumerable<QuestionnaireResponse.ItemComponent>? items, List<ExtractedAnswer> result)
    {
        if (items is null) return;

        foreach (var item in items)
        {
            if (string.IsNullOrEmpty(item.LinkId)) continue;

            var definition = index.Find(item.LinkId).FirstOrDefault();
            var key = index.ResolveKey(item.LinkId);

            foreach (var answer in item.Answer ?? [])
            {
                var row = BuildRow(item.LinkId, definition, key, answer);
                if (row is not null) result.Add(row);

                // LForms nests sub-items of a repeating group under the answer it belongs to
                Walk(index, answer.Item, result);
            }

            Walk(index, item.Item, result);
        }
    }

    private static ExtractedAnswer? BuildRow(string linkId, Questionnaire.ItemComponent? definition, ConceptKey key, QuestionnaireResponse.AnswerComponent answer)
    {
        var mapped = answer.Value switch
        {
            FhirBoolean b when b.Value.HasValue => ("boolean", b.Value.Value ? "true" : "false", (string?)null, (string?)null),
            Integer i when i.Value.HasValue => ("integer", i.Value.Value.ToString(CultureInfo.InvariantCulture), null, null),
            FhirDecimal d when d.Value.HasValue => ("decimal", d.Value.Value.ToString(CultureInfo.InvariantCulture), null, null),
            Quantity q => ("quantity", q.Value?.ToString(CultureInfo.InvariantCulture), null, q.Unit ?? q.Code ?? UnitFromDefinition(definition)),
            FhirString s when !string.IsNullOrEmpty(s.Value) => ("string", s.Value, null, null),
            FhirDateTime dt when !string.IsNullOrEmpty(dt.Value) => ("date", dt.Value, null, null),
            FhirCoding c => ("coding", CodingValue(c), c.Display ?? c.Code, null),
            CodeableConcept cc => CodingRow(cc),
            FhirUri u when !string.IsNullOrEmpty(u.Value) => ("string", u.Value, null, null),
            FhirUrl u when !string.IsNullOrEmpty(u.Value) => ("string", u.Value, null, null),
            Attachment a => ("attachment", a.Url ?? a.Title ?? a.ContentType, a.Title, null),
            _ => ((string?)null, (string?)null, (string?)null, (string?)null)
        };

        if (mapped.Item1 is null) return null;

        var codings = key.OtherCodings;
        return new ExtractedAnswer(linkId, key.System, key.Code, key.Display, codings,
            mapped.Item1, mapped.Item2, mapped.Item3, mapped.Item4);
    }

    private static (string?, string?, string?, string?) CodingRow(CodeableConcept concept)
    {
        var coding = concept.Coding?.OrderBy(QuestionnaireItemIndex.Rank).FirstOrDefault();
        if (coding is null) return ("string", concept.Text, concept.Text, null);
        return ("coding", CodingValue(coding), coding.Display ?? coding.Code, null);
    }

    private static string? CodingValue(FhirCoding? coding)
    {
        if (coding is null) return null;

        // LForms emits answer options as codings that carry only a display value
        if (string.IsNullOrEmpty(coding.System) && string.IsNullOrEmpty(coding.Code))
        {
            return coding.Display;
        }

        return $"{coding.System}|{coding.Code}";
    }

    private static string? UnitFromDefinition(Questionnaire.ItemComponent? definition)
        => definition?.Extension?.FirstOrDefault(e => e.Url == UnitOptionUrl)?.Value as FhirCoding is { } unit
            ? unit.Code ?? unit.Display
            : null;
}
