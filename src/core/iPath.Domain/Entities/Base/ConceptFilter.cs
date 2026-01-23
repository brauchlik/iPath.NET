
namespace iPath.Domain.Entities.Base;

public class ConceptFilter
{
    public bool IncludingChildCodes { get; set; } = true;
    public List<CodedConcept> Concetps { get; set; } = new();

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
}
