namespace iPath.Domain.Entities;

public class CodedConcept : IEquatable<CodedConcept>
{
    public const string IcodUrl = "http://terminology.hl7.org/CodeSystem/icd-o-3";


    public string System { get; set; }
    public string Code { get; set; }
    public string Display { get; set; }

    public override string ToString() => $"{Display} [{Code}]";

    public CodedConcept Clone() => (CodedConcept)MemberwiseClone();


    // Equality based on System and Code (case-insensitive)
    public bool Equals(CodedConcept? other)
    {
        if (ReferenceEquals(this, other)) return true;
        if (other is null) return false;

        return StringComparer.OrdinalIgnoreCase.Equals(System, other.System)
            && StringComparer.OrdinalIgnoreCase.Equals(Code, other.Code);
    }

    public override bool Equals(object? obj) => Equals(obj as CodedConcept);

    public override int GetHashCode()
    {
        var h = new HashCode();
        h.Add(System, StringComparer.OrdinalIgnoreCase);
        h.Add(Code, StringComparer.OrdinalIgnoreCase);
        return h.ToHashCode();
    }

    public static bool operator ==(CodedConcept? left, CodedConcept? right)
        => EqualityComparer<CodedConcept>.Default.Equals(left, right);

    public static bool operator !=(CodedConcept? left, CodedConcept? right)
        => !(left == right);

    // Provided comparer for collections
    public static IEqualityComparer<CodedConcept> Comparer { get; } = new CodedConceptEqualityComparer();

    private sealed class CodedConceptEqualityComparer : IEqualityComparer<CodedConcept>
    {
        public bool Equals(CodedConcept? x, CodedConcept? y)
        {
            if (ReferenceEquals(x, y)) return true;
            if (x is null || y is null) return false;
            return StringComparer.OrdinalIgnoreCase.Equals(x.System, y.System)
                && StringComparer.OrdinalIgnoreCase.Equals(x.Code, y.Code);
        }

        public int GetHashCode(CodedConcept obj)
        {
            if (obj is null) return 0;
            var h = new HashCode();
            h.Add(obj.System, StringComparer.OrdinalIgnoreCase);
            h.Add(obj.Code, StringComparer.OrdinalIgnoreCase);
            return h.ToHashCode();
        }
    }
}

