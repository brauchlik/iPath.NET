using Microsoft.AspNetCore.Components;

namespace iPath.Blazor.Componenents.Groups;

public class GroupListViewModel(IPathApi api, ISnackbar snackbar, IDialogService dialog, NavigationManager nm) : IViewModel
{
    public string SearchString { get; set; }

    public async Task<GridData<GroupListDto>> GetListAsync(GridState<GroupListDto> state)
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
    

    public void GotoGroup(GroupListDto group)
    {
        if (group != null)
        {
            nm.NavigateTo($"groups/{group.Id}");
        }
    }
}
