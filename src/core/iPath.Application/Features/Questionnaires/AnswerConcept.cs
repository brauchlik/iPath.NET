namespace iPath.Application.Features.Questionnaires;

/// <summary>
/// Identity and ordering rules for extracted answers, shared by the extractor, the pivot and the
/// catalog so they all group the same answer the same way.
/// </summary>
public static class AnswerConcept
{
    /// <summary>The identity of a concept: its code system and code.</summary>
    public static string KeyFor(string? codeSystem, string? code) => $"{codeSystem}|{code}";

    /// <summary>True for the key the extractor generated for an item that carries no coding.</summary>
    public static bool IsGeneratedKey(string? codeSystem)
        => string.Equals(codeSystem, QuestionnaireItemIndex.DummySystem, StringComparison.OrdinalIgnoreCase);

    /// <summary>Column order: SNOMED CT, then LOINC, then anything else, then generated keys.</summary>
    public static int SystemRank(string? codeSystem)
    {
        if (IsGeneratedKey(codeSystem)) return 3;
        if (codeSystem is null) return 2;
        if (codeSystem.Contains("snomed.info", StringComparison.OrdinalIgnoreCase)) return 0;
        if (codeSystem.Contains("loinc.org", StringComparison.OrdinalIgnoreCase)) return 1;
        return 2;
    }
}
