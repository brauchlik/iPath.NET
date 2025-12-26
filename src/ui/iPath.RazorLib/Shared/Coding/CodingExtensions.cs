using Hl7.Fhir.Model;



namespace iPath.Razorlib.Coding;

public static class CodingExtensions
{
    extension(CodeSystem.ConceptDefinitionComponent code)
    {
        public CodedConcept ToConcept()
            => new CodedConcept { Code = code.Code, Display = code.Display };
    }

    extension(CodedConcept concept)
    {
        public string ToDisplay()
            => concept is null ? "" : $"{concept.Display} [{concept.Code}]";

        public string ToAppend()
            => concept is null ? "" : $"- {concept.Display} [{concept.Code}]";
    }
}