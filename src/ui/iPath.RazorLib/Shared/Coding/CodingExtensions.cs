using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;



namespace iPath.Razorlib.Coding;

public static class CodingExtensions
{
    extension(CodeSystem.ConceptDefinitionComponent code)
    {
        public CodedConcept ToConcept()
            => new CodedConcept { Code = code.Code, Display = code.Display };


        public bool ContainsWordsStartingWith(string term)
        {
            if (string.IsNullOrWhiteSpace(term)) return false;

            var terms = term.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            bool hasUnmatchedTerm = false;

            foreach (var t in terms)
            {
                bool termMatched = false;
                var wordsInDisplay = code.Display.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                foreach (var word in wordsInDisplay)
                {
                    if (word.StartsWith(t, StringComparison.OrdinalIgnoreCase))
                        termMatched = true;
                }
                if (!termMatched)
                {
                    hasUnmatchedTerm = true;
                    break;
                }
            }

            return !hasUnmatchedTerm;
        }

    }

    extension(CodedConcept concept)
    {
        public string ToDisplay()
            => concept is null ? "" : $"{concept.Display} [{concept.Code}]";

        public string ToAppend()
            => concept is null ? "" : $"- {concept.Display} [{concept.Code}]";
    }
}