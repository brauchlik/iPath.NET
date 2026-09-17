using Hl7.Fhir.Model;
using iPath.Application.Features.Questionnaires.Queries;
using FhirCoding = Hl7.Fhir.Model.Coding;

namespace iPath.Application.Features.Questionnaires;

public record CatalogFormInput(string QuestionnaireId, string Name, int Version, Questionnaire Definition);

public record CatalogAnswerCounts(int Cases, int Rows);

/// <summary>
/// Turns the definitions of a group's case description forms into the catalog of concepts they can
/// record. Keys come from <see cref="QuestionnaireItemIndex"/> - the same rule the extractor uses -
/// so a catalog entry always lines up with the stored answers.
/// </summary>
public static class AnswerCatalogBuilder
{
    public static IReadOnlyList<CatalogConceptDto> BuildConcepts(
        IEnumerable<CatalogFormInput> forms,
        IReadOnlyDictionary<string, CatalogAnswerCounts>? counts = null)
    {
        var entries = new Dictionary<string, BuilderEntry>(StringComparer.OrdinalIgnoreCase);

        foreach (var form in forms)
        {
            if (form.Definition is null) continue;

            var index = QuestionnaireItemIndex.Build(form.Definition);
            foreach (var item in Walk(form.Definition.Item))
            {
                if (string.IsNullOrEmpty(item.LinkId)) continue;
                if (item.Type is null or Questionnaire.QuestionnaireItemType.Group or Questionnaire.QuestionnaireItemType.Display) continue;

                var key = index.ResolveKey(item.LinkId);
                var id = AnswerConcept.KeyFor(key.System, key.Code);
                var valueType = item.Type.ToString()?.ToLowerInvariant();

                if (!entries.TryGetValue(id, out var entry))
                {
                    entry = new BuilderEntry
                    {
                        Key = id,
                        CodeSystem = key.System,
                        Code = key.Code,
                        Display = key.Display ?? item.LinkId
                    };
                    entries[id] = entry;
                }

                entry.Display ??= key.Display ?? item.LinkId;
                entry.ValueType ??= valueType;
                entry.Repeats |= item.Repeats == true;
                entry.LinkIds.Add(item.LinkId);
                entry.Forms.Add(form.QuestionnaireId);
                foreach (var option in Options(item)) entry.Options.Add(option);
            }
        }

        return entries.Values
            .Select(e =>
            {
                CatalogAnswerCounts? answered = null;
                counts?.TryGetValue(e.Key, out answered);

                return new CatalogConceptDto(
                    e.Key, e.CodeSystem, e.Code, e.Display ?? e.Key,
                    !AnswerConcept.IsGeneratedKey(e.CodeSystem),
                    e.ValueType, e.Repeats,
                    e.Options.OrderBy(o => o, StringComparer.OrdinalIgnoreCase).ToList(),
                    e.LinkIds.OrderBy(l => l, StringComparer.OrdinalIgnoreCase).ToList(),
                    e.Forms.OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList(),
                    answered?.Cases ?? 0, answered?.Rows ?? 0);
            })
            .OrderBy(c => AnswerConcept.SystemRank(c.CodeSystem))
            .ThenBy(c => c.Display, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IEnumerable<Questionnaire.ItemComponent> Walk(IEnumerable<Questionnaire.ItemComponent>? items)
    {
        if (items is null) yield break;

        foreach (var item in items)
        {
            yield return item;
            foreach (var child in Walk(item.Item)) yield return child;
        }
    }

    /// <summary>The values a question offers, so the catalog says what a column can contain.</summary>
    private static IEnumerable<string> Options(Questionnaire.ItemComponent item)
    {
        if (!string.IsNullOrEmpty(item.AnswerValueSet)) yield return $"[valueset] {item.AnswerValueSet}";

        foreach (var option in item.AnswerOption ?? [])
        {
            var text = option.Value switch
            {
                FhirString s => s.Value,
                FhirCoding c => c.Display ?? c.Code,
                Integer i => i.Value?.ToString(),
                FhirDecimal d => d.Value?.ToString(),
                FhirBoolean b => b.Value?.ToString(),
                _ => option.Value?.ToString()
            };

            if (!string.IsNullOrWhiteSpace(text)) yield return text.Trim();
        }
    }

    private class BuilderEntry
    {
        public string Key { get; init; } = string.Empty;
        public string? CodeSystem { get; init; }
        public string? Code { get; init; }
        public string? Display { get; set; }
        public string? ValueType { get; set; }
        public bool Repeats { get; set; }
        public HashSet<string> Options { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> LinkIds { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> Forms { get; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
