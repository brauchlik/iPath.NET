using Hl7.Fhir.Model;

namespace iPath.Application.Features.Questionnaires;

// generic suggestion by copilot
public class GenericQuestionnaireToTextService : IQuestionnaireToTextService
{
    public string CreateText(QuestionnaireResponse response, Questionnaire questionnaire)
    {
        if (response is null) return string.Empty;

        // build map linkId -> list of response items for quick lookup
        var respMap = new Dictionary<string, List<QuestionnaireResponse.ItemComponent>>(StringComparer.OrdinalIgnoreCase);
        void BuildResponseMap(IEnumerable<QuestionnaireResponse.ItemComponent>? items)
        {
            if (items is null) return;
            foreach (var it in items)
            {
                if (string.IsNullOrEmpty(it.LinkId)) continue;
                if (!respMap.TryGetValue(it.LinkId, out var list))
                {
                    list = new List<QuestionnaireResponse.ItemComponent>();
                    respMap[it.LinkId] = list;
                }
                list.Add(it);
                BuildResponseMap(it.Item);
            }
        }
        BuildResponseMap(response.Item);

        var pairs = new List<string>();

        // helper: check whether a questionnaire item (or any descendant) has at least one filled answer
        bool ItemHasAnswers(Questionnaire.ItemComponent? q)
        {
            if (q is null) return false;

            if (!string.IsNullOrEmpty(q.LinkId) && respMap.TryGetValue(q.LinkId, out var responses))
            {
                if (responses.Any(r => r.Answer != null && r.Answer.Any(a => a.Value != null)))
                    return true;
            }

            if (q.Item != null)
            {
                foreach (var child in q.Item)
                {
                    if (ItemHasAnswers(child)) return true;
                }
            }

            return false;
        }

        // collect question = answer pairs (only items with filled answers)
        void CollectItems(IEnumerable<Questionnaire.ItemComponent>? items)
        {
            if (items is null) return;
            foreach (var q in items)
            {
                if (!ItemHasAnswers(q)) continue;

                // If this item itself has answers, produce a pair for it
                if (!string.IsNullOrEmpty(q.LinkId) && respMap.TryGetValue(q.LinkId, out var responses))
                {
                    var allAnswers = new List<string>();
                    foreach (var r in responses)
                    {
                        if (r.Answer == null) continue;
                        foreach (var a in r.Answer.Where(a => a.Value != null))
                        {
                            var ansText = AnswerToString(a);
                            if (!string.IsNullOrEmpty(ansText))
                                allAnswers.Add(ansText);
                        }
                    }

                    if (allAnswers.Any())
                    {
                        var questionText = !string.IsNullOrEmpty(q.Text) ? q.Text : $"[{q.LinkId}]";
                        var answersCombined = string.Join("; ", allAnswers);
                        pairs.Add($"{questionText} = {answersCombined}");
                    }
                }

                // recurse into children (they will only be processed if they have answers)
                if (q.Item != null)
                {
                    CollectItems(q.Item);
                }
            }
        }

        CollectItems(questionnaire?.Item);

        // Fallback: if nothing collected from questionnaire structure, inspect raw response items
        if (!pairs.Any() && response.Item != null)
        {
            void CollectRawResponses(IEnumerable<QuestionnaireResponse.ItemComponent> items)
            {
                foreach (var r in items)
                {
                    bool ResponseItemHasAnswers(QuestionnaireResponse.ItemComponent item)
                    {
                        if (item.Answer != null && item.Answer.Any(a => a.Value != null)) return true;
                        if (item.Item != null)
                        {
                            foreach (var child in item.Item)
                                if (ResponseItemHasAnswers(child)) return true;
                        }
                        return false;
                    }

                    if (!ResponseItemHasAnswers(r)) continue;

                    var title = !string.IsNullOrEmpty(r.LinkId) ? $"[{r.LinkId}]" : "(item)";

                    var allAnswers = new List<string>();
                    if (r.Answer != null)
                    {
                        foreach (var a in r.Answer.Where(a => a.Value != null))
                        {
                            var ansText = AnswerToString(a);
                            if (!string.IsNullOrEmpty(ansText))
                                allAnswers.Add(ansText);
                        }
                    }

                    if (allAnswers.Any())
                    {
                        pairs.Add($"{title} = {string.Join("; ", allAnswers)}");
                    }

                    if (r.Item != null)
                        CollectRawResponses(r.Item);
                }
            }

            CollectRawResponses(response.Item);
        }

        // join pairs as: question = answer, question2 = answer2, ...
        return string.Join(", ", pairs);
    }

    private static string AnswerToString(QuestionnaireResponse.AnswerComponent ans)
    {
        if (ans == null || ans.Value == null) return string.Empty;

        var v = ans.Value;

        // primitive wrappers in Hl7.Fhir.Model (R4)
        if (v is FhirString fs) return fs.Value ?? string.Empty;
        if (v is FhirBoolean fb) return fb.Value?.ToString() ?? string.Empty;
        if (v is FhirDecimal fd) return fd.Value?.ToString() ?? string.Empty;
        if (v is FhirDateTime fdt) return fdt.Value ?? string.Empty;
        if (v is FhirUri fu) return fu.Value ?? string.Empty;

        // complex types
        if (v is Hl7.Fhir.Model.Coding coding)
        {
            // show only the answer (display or code) and do NOT include code system
            if (!string.IsNullOrEmpty(coding.Display)) return coding.Display;
            if (!string.IsNullOrEmpty(coding.Code)) return coding.Code;
            return string.Empty;
        }

        if (v is Quantity q)
        {
            var value = q.Value?.ToString() ?? string.Empty;
            var unit = q.Unit ?? q.Code ?? string.Empty;
            return string.IsNullOrEmpty(unit) ? value : $"{value} {unit}";
        }

        if (v is Attachment a)
        {
            if (!string.IsNullOrEmpty(a.Title)) return a.Title;
            if (!string.IsNullOrEmpty(a.Url)) return a.Url;
            return a.ContentType ?? string.Empty;
        }

        if (v is ResourceReference rr)
        {
            return rr.Display ?? rr.Reference ?? string.Empty;
        }

        // fallback to generic ToString
        return v.ToString() ?? string.Empty;
    }
}