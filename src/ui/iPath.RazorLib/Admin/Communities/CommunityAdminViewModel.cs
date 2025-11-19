namespace iPath.Blazor.Componenents.Admin.Communities;

public class CommunityAdminViewModel(IPathApi api, ISnackbar snackbar, IDialogService dialog) : IViewModel
{
    public async Task<GridData<CommunityListDto>> GetListAsync(GridState<CommunityListDto> state)
    {
        var query = state.BuildQuery(new GetCommunityListQuery());
        var resp = await api.GetCommunityList(query);

        if (resp.IsSuccessful) return resp.Content.ToGridData();

        snackbar.AddWarning(resp.ErrorMessage);
        return new GridData<CommunityListDto>();
    }


    public CommunityListDto SelectedItem { get; private set; }
    public async Task SetSelectedItem(CommunityListDto item)
    {
        if (SelectedItem == item) return;
        SelectedItem = item;
        await LoadCommunity(item?.Id);
    }

    public string SelectedRowStyle(CommunityListDto item, int rowIndex)
    {
        if (item is not null && SelectedItem is not null && item.Id == SelectedItem.Id )
            return "background-color: var(--mud-palette-background-gray)";

        return "";
    }



    public CommunityDto SelectedCommunity { get; private set; }
    private async Task LoadCommunity(Guid? id)
    {
        if (!id.HasValue)
        {
            SelectedCommunity = null;
        }
        else
        {
            var resp = await api.GetCommunity(id.Value);
            if (resp.IsSuccessful)
            {
                SelectedCommunity = resp.Content;
            }
            snackbar.AddError(resp.ErrorMessage);
        }
    }


    public async Task Create()
    {
        if (SelectedItem != null)
        {
            snackbar.AddWarning("not implemented yet");
        }
    }

    public async Task Edit()
    {
        if (SelectedItem != null)
        {
            snackbar.AddWarning("not implemented yet");
        }
    }

    public async Task Delete()
    {
        if (SelectedItem != null)
        {
            snackbar.AddWarning("not implemented yet");
        }
    }


    public async Task AddGroups(GroupListDto group)
    {
        snackbar.AddWarning("not implemented yet");
    }

    public async Task RemnoveGroups(GroupListDto group)
    {
        snackbar.AddWarning("not implemented yet");
    }
}
