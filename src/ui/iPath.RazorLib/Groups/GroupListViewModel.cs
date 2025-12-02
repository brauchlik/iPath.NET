using iPath.Blazor.ServiceLib.ApiClient;
using Microsoft.AspNetCore.Components;

namespace iPath.Blazor.Componenents.Groups;

public class GroupListViewModel(IPathApi api, ISnackbar snackbar, IDialogService dialog, NavigationManager nm) : IViewModel
{
    public CommunityListDto? SelectedCommunity {  get; set; }
    public string SearchString { get; set; }

    public async Task<GridData<GroupListDto>> GetGridAsync(GridState<GroupListDto> state)
    {
        var query = state.BuildQuery(new GetGroupListQuery { IncludeCounts = true });
        var resp = await api.GetGroupList(query);
        if (resp.IsSuccessful)
        {
            return resp.Content.ToGridData();
        }

        snackbar.AddError(resp.ErrorMessage);
        return new GridData<GroupListDto>();
    }


    public async Task<TableData<GroupListDto>> GetTableAsync(TableState state, CancellationToken ct)
    {
        var query = state.BuildQuery(new GetGroupListQuery { IncludeCounts = true });
        var resp = await api.GetGroupList(query);
        if (resp.IsSuccessful)
        {
            return resp.Content.ToTableData();
        }

        snackbar.AddError(resp.ErrorMessage);
        return new TableData<GroupListDto>();
    }


    public void GotoGroup(GroupListDto group)
    {
        if (group != null)
        {
            nm.NavigateTo($"groups/{group.Id}");
        }
    }


    public async Task<IEnumerable<GroupListDto>> Search(string? search, Guid? CommunityId,  CancellationToken ct)
    {
        if (search is not null)
        {
            var query = new GetGroupListQuery { SearchString = search, Page = 0, PageSize = 100, CommunityId = CommunityId };
            var resp = await api.GetGroupList(query);
            if (resp.IsSuccessful)
            {
                return resp.Content.Items.OrderBy(g => g.Name);
            }
            else
            {
                snackbar.AddWarning(resp.ErrorMessage);
            }
        }
        return new List<GroupListDto>();
    }
}
