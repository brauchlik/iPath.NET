namespace iPath.Application.Querying;

public class Filter<T> where T : class
{
    public string Column { get; set; } = "";
    public string Value { get; set; } = "";
    public string Operator { get; set; } = "";
}