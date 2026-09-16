using Hl7.Fhir.Model;
using System.Text;

namespace iPath.Application.Features.Questionnaires;

public enum CaseDescriptionOutputMode
{
    Compact,
    Expanded
}

public class CaseDescriptionToTextService(CaseDescriptionOutputMode mode) : IQuestionnaireToTextService
{
    public string CreateText(QuestionnaireResponse response, Questionnaire questionnaire)
    {
        if (response is null || questionnaire is null) return string.Empty;

        var respMap = BuildResponseMap(response.Item);

        var sb = new StringBuilder();
        var firstGroup = true;

        foreach (var group in questionnaire.Item.Where(i => i.Type == Questionnaire.QuestionnaireItemType.Group))
        {
            var groupEntries = new List<string>();
            var groupSubLines = new List<string>();

            ProcessGroupItems(group.Item, respMap, groupEntries, groupSubLines);

            if (groupEntries.Count == 0) continue;

            if (!firstGroup)
            {
                sb.Append("<br/><br/>");
            }
            firstGroup = false;

            var groupLabel = !string.IsNullOrEmpty(group.Text) ? group.Text : $"[{group.LinkId}]";
            var entries = string.Join(", ", groupEntries);
            sb.Append($"<strong>{groupLabel}:</strong> {entries}");

            if (mode == CaseDescriptionOutputMode.Expanded && groupSubLines.Count > 0)
            {
                sb.Append("<br/>");
                foreach (var subLine in groupSubLines)
                {
                    sb.Append($"{subLine}");
                    sb.Append("<br/>");
                }
            }
        }

        return sb.ToString().TrimEnd();
    }

    private void ProcessGroupItems(
        IEnumerable<Questionnaire.ItemComponent>? items,
        Dictionary<string, List<QuestionnaireResponse.ItemComponent>> respMap,
        List<string> entries,
        List<string> subLines)
    {
        if (items is null) return;

        foreach (var q in items)
        {
            if (string.IsNullOrEmpty(q.LinkId)) continue;
            if (!respMap.TryGetValue(q.LinkId, out var responses)) continue;

            var hasAnswers = responses.Any(r => r.Answer != null && r.Answer.Any(a => a.Value != null));
            if (!hasAnswers) continue;

            switch (q.Type)
            {
                case Questionnaire.QuestionnaireItemType.Boolean:
                    ProcessBooleanItem(q, responses, entries, subLines, respMap);
                    break;

                case Questionnaire.QuestionnaireItemType.Choice:
                case Questionnaire.QuestionnaireItemType.OpenChoice:
                    ProcessChoiceItem(q, responses, entries);
                    break;

                case Questionnaire.QuestionnaireItemType.Quantity:
                    ProcessQuantityItem(q, responses, entries);
                    break;

                case Questionnaire.QuestionnaireItemType.Group:
                    ProcessGroupItems(q.Item, respMap, entries, subLines);
                    break;
            }
        }
    }

    private void ProcessBooleanItem(
        Questionnaire.ItemComponent q,
        List<QuestionnaireResponse.ItemComponent> responses,
        List<string> entries,
        List<string> subLines,
        Dictionary<string, List<QuestionnaireResponse.ItemComponent>> respMap)
    {
        var isTrue = responses.Any(r =>
            r.Answer != null && r.Answer.Any(a =>
                a.Value is FhirBoolean fb && fb.Value == true));

        if (!isTrue) return;

        var label = q.Text ?? $"[{q.LinkId}]";

        if (q.Item == null || q.Item.Count == 0)
        {
            entries.Add(label);
            return;
        }

        var subValues = CollectSubItemValues(q.Item, respMap);
        if (subValues.Count == 0)
        {
            entries.Add(label);
            return;
        }

        var subCombined = string.Join(", ", subValues);

        if (mode == CaseDescriptionOutputMode.Compact)
        {
            entries.Add($"{label} ({subCombined})");
            return;
        }

        if (subValues.Count > 0)
        {
            subLines.Add($"  - {label} \u2192 {subCombined}<br/>");
        }
        else
        {
            entries.Add(label);
        }
    }

    private List<string> CollectSubItemValues(
        IEnumerable<Questionnaire.ItemComponent> subItems,
        Dictionary<string, List<QuestionnaireResponse.ItemComponent>> respMap)
    {
        var values = new List<string>();

        foreach (var sub in subItems)
        {
            if (string.IsNullOrEmpty(sub.LinkId)) continue;
            if (!respMap.TryGetValue(sub.LinkId, out var subResponses)) continue;

            var hasAnswers = subResponses.Any(r => r.Answer != null && r.Answer.Any(a => a.Value != null));
            if (!hasAnswers) continue;

            switch (sub.Type)
            {
                case Questionnaire.QuestionnaireItemType.Boolean:
                    var isTrue = subResponses.Any(r =>
                        r.Answer != null && r.Answer.Any(a =>
                            a.Value is FhirBoolean fb && fb.Value == true));
                    if (isTrue)
                        values.Add(sub.Text ?? $"[{sub.LinkId}]");
                    break;

                case Questionnaire.QuestionnaireItemType.Choice:
                case Questionnaire.QuestionnaireItemType.OpenChoice:
                    foreach (var r in subResponses)
                    {
                        if (r.Answer == null) continue;
                        foreach (var a in r.Answer.Where(a => a.Value != null))
                        {
                            var v = AnswerToString(a);
                            if (!string.IsNullOrEmpty(v))
                                values.Add(v);
                        }
                    }
                    break;

                case Questionnaire.QuestionnaireItemType.Quantity:
                    foreach (var r in subResponses)
                    {
                        if (r.Answer == null) continue;
                        foreach (var a in r.Answer.Where(a => a.Value != null))
                        {
                            var v = QuantityToString(a);
                            if (!string.IsNullOrEmpty(v))
                                values.Add(v);
                        }
                    }
                    break;
            }
        }

        return values;
    }

    private void ProcessChoiceItem(
        Questionnaire.ItemComponent q,
        List<QuestionnaireResponse.ItemComponent> responses,
        List<string> entries)
    {
        foreach (var r in responses)
        {
            if (r.Answer == null) continue;
            foreach (var a in r.Answer.Where(a => a.Value != null))
            {
                var v = AnswerToString(a);
                if (!string.IsNullOrEmpty(v))
                    entries.Add(v);
            }
        }
    }

    private void ProcessQuantityItem(
        Questionnaire.ItemComponent q,
        List<QuestionnaireResponse.ItemComponent> responses,
        List<string> entries)
    {
        foreach (var r in responses)
        {
            if (r.Answer == null) continue;
            foreach (var a in r.Answer.Where(a => a.Value != null))
            {
                var v = QuantityToString(a);
                if (!string.IsNullOrEmpty(v))
                    entries.Add(v);
            }
        }
    }

    private static Dictionary<string, List<QuestionnaireResponse.ItemComponent>> BuildResponseMap(
        IEnumerable<QuestionnaireResponse.ItemComponent>? items)
    {
        var map = new Dictionary<string, List<QuestionnaireResponse.ItemComponent>>(StringComparer.OrdinalIgnoreCase);
        if (items == null) return map;

        void Walk(IEnumerable<QuestionnaireResponse.ItemComponent>? current)
        {
            if (current == null) return;
            foreach (var it in current)
            {
                if (!string.IsNullOrEmpty(it.LinkId))
                {
                    if (!map.TryGetValue(it.LinkId, out var list))
                    {
                        list = new List<QuestionnaireResponse.ItemComponent>();
                        map[it.LinkId] = list;
                    }
                    list.Add(it);
                }
                Walk(it.Item);
                if (it.Answer != null)
                {
                    foreach (var a in it.Answer)
                    {
                        Walk(a.Item);
                    }
                }
            }
        }

        Walk(items);
        return map;
    }

    private static string AnswerToString(QuestionnaireResponse.AnswerComponent ans)
    {
        if (ans?.Value == null) return string.Empty;

        var v = ans.Value;

        if (v is FhirString fs) return fs.Value ?? string.Empty;
        if (v is FhirBoolean fb) return fb.Value?.ToString() ?? string.Empty;
        if (v is FhirDecimal fd) return fd.Value?.ToString() ?? string.Empty;
        if (v is FhirDateTime fdt) return fdt.Value ?? string.Empty;
        if (v is Hl7.Fhir.Model.Coding coding)
        {
            if (!string.IsNullOrEmpty(coding.Display)) return coding.Display;
            if (!string.IsNullOrEmpty(coding.Code)) return coding.Code;
        }
        if (v is ResourceReference rr)
        {
            return rr.Display ?? rr.Reference ?? string.Empty;
        }

        return v.ToString() ?? string.Empty;
    }

    private static string QuantityToString(QuestionnaireResponse.AnswerComponent ans)
    {
        if (ans?.Value == null) return string.Empty;
        if (ans.Value is not Quantity q) return string.Empty;

        var value = q.Value?.ToString() ?? string.Empty;
        var unit = q.Unit ?? q.Code ?? string.Empty;
        return string.IsNullOrEmpty(unit) ? value : $"{value} {unit}";
    }
}
