using System.ComponentModel;

namespace iPath.Application.Querying;

public record PagedResultList<T>(int TotalItems, IEnumerable<T> Items) where T : class;


public class PageParams
{
    [DefaultValue(0)]
    public int Page { get; set; } = 0;

    [DefaultValue(10)]
    public int? PageSize { get; set; } = 10;
}

public class PagedQuery : PageParams
{
    [DefaultValue(null)]
    public string[]? Sorting { get; set; }

    [DefaultValue(null)]
    public string[]? Filter { get; set; }
}


public class PagedQuery<T> : PagedQuery where T : class
{
}
