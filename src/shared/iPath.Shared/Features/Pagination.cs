using System.ComponentModel;

namespace iPath.Shared.Features;

public record PagedResult<T>(int TotalItems, IEnumerable<T> Items) where T : class;

public class PagedQuery<T> where T : class
{
    [DefaultValue(0)]
    public int Page { get; set; } = 0;

    [DefaultValue(10)]
    public int? PageSize { get; set; } = 10;

    [DefaultValue(null)]
    public Sorting<T>[]? Sorting { get; set; }

    [DefaultValue(null)]
    public Filter<T>[]? Filter { get; set; }
}

public class Sorting<T> where T : class
{
    public string Column { get; set; } = "";
    public bool Descending { get; set; } = false;
}

public class Filter<T> where T : class
{
    public string Column { get; set; } = "";
    public string Value { get; set; } = "";
    public string Operator { get; set; } = "";
}