namespace iPath.Domain.Entities;

public class CodedConcept
{
    public string System { get; set; }
    public string Code { get; set; }
    public string Display { get; set; }

    public override string ToString() => $"{Display} [{Code}]";

    public CodedConcept Clone() => (CodedConcept)MemberwiseClone();
}

