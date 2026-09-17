using Hl7.Fhir.Model;
using FhirCoding = Hl7.Fhir.Model.Coding;

namespace iPath.Application.Features.Questionnaires;

public record ConceptKey(string? System, string? Code, string? Display, string? OtherCodings);

/// <summary>
/// Shared view over a Questionnaire definition: linkId lookup, coding preference and the
/// concept-key resolution that both the answer extractor and the conformity checker use.
/// </summary>
public class QuestionnaireItemIndex
{
    public const string DummySystem = "http://ipath.net/fhir/CodeSystem/questionnaire-item";
    public const string SnomedSystem = "http://snomed.info/sct";
    public const string LoincSystem = "http://loinc.org";

    private readonly Dictionary<string, List<Questionnaire.ItemComponent>> _items = new();

    public static QuestionnaireItemIndex Build(Questionnaire? questionnaire)
    {
        var index = new QuestionnaireItemIndex();
        index.Add(questionnaire?.Item);
        return index;
    }

    private void Add(IEnumerable<Questionnaire.ItemComponent>? items)
    {
        if (items is null) return;
        foreach (var item in items)
        {
            if (!string.IsNullOrEmpty(item.LinkId))
            {
                if (!_items.TryGetValue(item.LinkId, out var list))
                {
                    list = [];
                    _items[item.LinkId] = list;
                }
                list.Add(item);
            }
            Add(item.Item);
        }
    }

    public IReadOnlyList<Questionnaire.ItemComponent> Find(string? linkId)
        => linkId is not null && _items.TryGetValue(linkId, out var list) ? list : [];

    public IEnumerable<Questionnaire.ItemComponent> AllItems => _items.Values.SelectMany(v => v);

    public IEnumerable<string> DuplicateLinkIds => _items.Where(kv => kv.Value.Count > 1).Select(kv => kv.Key);

    /// <summary>Coding preference for the concept key: SNOMED CT, then LOINC, then anything else.</summary>
    public static int Rank(FhirCoding? coding)
    {
        var system = coding?.System ?? string.Empty;
        if (system.Contains("snomed.info", StringComparison.OrdinalIgnoreCase)) return 0;
        if (system.Contains("loinc.org", StringComparison.OrdinalIgnoreCase)) return 1;
        return 2;
    }

    public static string FormatCoding(FhirCoding coding)
    {
        var display = coding.Display ?? coding.Code;
        return display is null
            ? $"{coding.System}|{coding.Code}"
            : $"{coding.System}|{coding.Code}|{display}";
    }

    public static (FhirCoding? Preferred, string? Others) PreferredCodings(Questionnaire.ItemComponent? item)
    {
        var codings = item?.Code?.Where(c => !string.IsNullOrEmpty(c.Code)).ToList();
        if (codings is null || codings.Count == 0) return (null, null);

        var preferred = codings.OrderBy(Rank).First();
        var others = codings.Where(c => !ReferenceEquals(c, preferred)).Select(FormatCoding).ToList();
        return (preferred, others.Count == 0 ? null : string.Join(";", others));
    }

    /// <summary>
    /// Resolves the concept key for a linkId. Coded items use their own preferred coding; uncoded
    /// items inherit the nearest coded ancestor's coding with the remaining path appended
    /// (so sym.pain.abdominal.duration and sym.other.pain.abdominal.duration share one key);
    /// with no coded ancestor the full linkId becomes the key under the dummy system.
    /// </summary>
    public ConceptKey ResolveKey(string? linkId)
    {
        if (string.IsNullOrEmpty(linkId)) return new ConceptKey(DummySystem, linkId, null, null);

        var (own, ownOthers) = PreferredCodings(Find(linkId).FirstOrDefault());
        if (own is not null)
            return new ConceptKey(own.System, own.Code, own.Display ?? own.Code, ownOthers);

        var segments = linkId.Split('.');
        for (var take = segments.Length - 1; take >= 1; take--)
        {
            var ancestorLinkId = string.Join('.', segments.Take(take));
            var (ancestor, ancestorOthers) = PreferredCodings(Find(ancestorLinkId).FirstOrDefault());
            if (ancestor is not null)
            {
                var remainder = string.Join('.', segments.Skip(take));
                return new ConceptKey(
                    ancestor.System,
                    $"{ancestor.Code}.{remainder}",
                    ancestor.Display is null ? remainder : $"{ancestor.Display} ({remainder})",
                    ancestorOthers);
            }
        }

        return new ConceptKey(DummySystem, linkId, null, null);
    }
}
