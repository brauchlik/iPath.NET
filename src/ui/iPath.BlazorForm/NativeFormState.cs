using Hl7.Fhir.Model;

namespace iPath.BlazorForm;

// Shared mutable state for one form instance: current answers keyed by linkId, plus enableWhen
// evaluation. Passed by reference down the QuestionnaireItemView tree so a change anywhere can
// affect visibility anywhere else, without prop-drilling every value individually.
public class NativeFormState
{
    public Dictionary<string, List<DataType>> Answers { get; } = new();
    public bool ReadOnly { get; set; }
    public Action? OnChanged { get; set; }

    public List<DataType> GetAnswers(string linkId) =>
        Answers.TryGetValue(linkId, out var list) ? list : [];

    public void SetAnswers(string linkId, List<DataType> values)
    {
        Answers[linkId] = values;
        OnChanged?.Invoke();
    }

    public bool IsEnabled(Questionnaire.ItemComponent item)
    {
        if (item.EnableWhen is not { Count: > 0 }) return true;
        return item.EnableWhen.All(Evaluate);
    }

    bool Evaluate(Questionnaire.EnableWhenComponent condition)
    {
        var current = GetAnswers(condition.Question);
        if (condition.Operator == Questionnaire.QuestionnaireItemOperator.Exists)
        {
            var expected = (condition.Answer as FhirBoolean)?.Value ?? true;
            return (current.Count > 0) == expected;
        }
        // Only "=" is meaningful for this stub; other operators (>, <, etc.) are treated as
        // not satisfied rather than guessed at.
        if (condition.Operator != Questionnaire.QuestionnaireItemOperator.Equal) return false;
        return condition.Answer is not null && current.Any(a => ValuesEqual(a, condition.Answer));
    }

    static bool ValuesEqual(DataType a, DataType b) => (a, b) switch
    {
        (FhirBoolean x, FhirBoolean y) => x.Value == y.Value,
        (FhirString x, FhirString y) => x.Value == y.Value,
        (Coding x, Coding y) => (!string.IsNullOrEmpty(x.Code) && x.Code == y.Code)
            || (!string.IsNullOrEmpty(x.Display) && x.Display == y.Display),
        _ => false
    };
}
