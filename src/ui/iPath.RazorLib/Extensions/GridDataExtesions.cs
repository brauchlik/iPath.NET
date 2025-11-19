using iPath.Application.Querying;
using MudBlazor;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace iPath.Blazor.Componenents.Extensions;

internal static  class GridDataExtesions
{
    public static GridData<T> ToGridData<T>(this PagedResultList<T> data) where T : class
    {
        return new GridData<T> { TotalItems = data.TotalItems, Items = data.Items };
    }


    public static TQuery BuildQuery<TQuery, T>(this GridState<T> state, TQuery query) where T : class where TQuery : PagedQuery
    {
        query.Page = state.Page;
        query.PageSize = state.PageSize;
        query.Sorting = state.ToSorting();
        return query;
    }

    public static string[]? ToSorting<T>(this GridState<T> state) where T : class
    {
        var ret = new List<string>();

        if (state.SortDefinitions != null)
        {
            foreach (var sd in state.SortDefinitions)
            {
                ret.Add(sd.SortBy + (sd.Descending ? " DESC" : " ASC"));
            }
        }

        return ret.ToArray();
    }




    public static TQuery BuildQuery<TQuery>(this TableState state, TQuery query) where TQuery : PagedQuery
    {
        query.Page = state.Page;
        query.PageSize = state.PageSize;
        query.Sorting = state.ToSorting();
        return query;
    }

    public static string[]? ToSorting(this TableState state)
    {
        var ret = new List<string>();

        if (!string.IsNullOrEmpty(state.SortLabel))
        {
            ret.Add(state.SortLabel + (state.SortDirection == SortDirection.Descending  ? " DESC" : " ASC"));
        }

        return ret.ToArray();
    }

    public static TableData<T> ToTableData<T>(this PagedResultList<T> data) where T : class
    {
        return new TableData<T> { TotalItems = data.TotalItems, Items = data.Items };
    }
}
