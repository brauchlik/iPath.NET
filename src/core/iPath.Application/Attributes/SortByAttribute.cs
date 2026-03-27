namespace iPath.Application.Attributes;

[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public class SortByAttribute : Attribute
{
    public string SortBy { get; }
    public string EntityField { get; }

    public SortByAttribute(string sortBy, string entityField)
    {
        SortBy = sortBy;
        EntityField = entityField;
    }
}
