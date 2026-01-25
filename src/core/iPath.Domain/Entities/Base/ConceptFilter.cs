
using System.Diagnostics;

namespace iPath.Domain.Entities.Base;


[DebuggerDisplay("Filter - {ConceptCodesString}")]
public class ConceptFilter
{
    public List<CodedConcept> Concetps { get; set; } = new();
    // public bool IncludingChildCodes { get; set; } = true;

    public void Add(CodedConcept newValue)
    {
        if (!Concetps.Any(x => x.Equals(newValue)))
        {
            Concetps.Add(newValue.Clone());
        }
    }

    public void Remove(CodedConcept c)
    {
        Concetps.RemoveAll(x => x.Equals(c));
    }

    [JsonIgnore]
    public HashSet<string> ConceptCodes => Concetps is null ?
        new HashSet<string>() :
        Concetps.Select(x => x.Code).ToHashSet();

    [JsonIgnore]
    public string ConceptCodesString => string.Join(", ", ConceptCodes); 
}
