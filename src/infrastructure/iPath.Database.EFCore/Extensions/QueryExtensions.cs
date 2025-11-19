using Microsoft.EntityFrameworkCore.Query;
using System.Linq.Dynamic.Core;

namespace iPath.EF.Core;

internal static class QueryExtensions
{


    public static IQueryable<T> ApplyQuery<T>(this IQueryable<T> q, PagedQuery query) where T : class
    {
        if (query.Sorting is not null)
        {
            foreach (var sd in query.Sorting)
            {
                if (!string.IsNullOrWhiteSpace(sd))
                {
                    q = q.OrderBy(sd);
                }
            }
        }
        return q;
    }


    public static async Task<PagedResultList<T>> ToPagedResultAsync<T>(this IQueryable<T> q, PageParams pageParams, CancellationToken ct) where T : class
    {
        return new PagedResultList<T>(
            TotalItems: await q.CountAsync(ct),
            Items: await q.GetPageAsync(pageParams, ct));
    }

    public static async Task<IEnumerable<T>> GetPageAsync<T>(this IQueryable<T> q, PageParams p, CancellationToken ct) where T : class
    {
        if (p.PageSize.HasValue)
        {
            q = q.Skip(p.Page * p.PageSize.Value)
                .Take(p.PageSize.Value);
        }
        return await q.ToArrayAsync(ct);
    }


}
