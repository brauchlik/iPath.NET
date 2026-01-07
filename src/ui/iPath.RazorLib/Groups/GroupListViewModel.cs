using iPath.Blazor.ServiceLib.ApiClient;
using Microsoft.AspNetCore.Components;

namespace iPath.Blazor.Componenents.Groups;

public class GroupListViewModel(IPathApi api, GroupCache cache, ISnackbar snackbar, IDialogService dialog, NavigationManager nm) : IViewModel
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
            GroupCount = resp.Content.TotalItems;
            return resp.Content.ToTableData();
        }

        snackbar.AddError(resp.ErrorMessage);
        return new TableData<GroupListDto>();
    }

    public int GroupCount { get; set; }


    public void GotoGroup(Guid groupId)
    {
        if (groupId != Guid.Empty)
        {
            nm.NavigateTo($"groups/{groupId}");
        }
    }

    public void GotoGroup(GroupListDto group)
    {
        if (group != null)
        {
            GotoGroup(group.Id);
        }
    }

    public async Task<GroupDto> GetGroupDto(Guid groupId)
    {
        return await cache.GetGroupAsync(groupId);
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
