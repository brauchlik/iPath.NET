namespace iPath.Blazor.Componenents.Admin.Users;

public class UserAdminViewModel(IPathApi api, ISnackbar snackbar, IDialogService dialog) : IViewModel
{

    public string SearchString { get; set; } = "";

    public async Task<GridData<UserListDto>> GetListAsync(GridState<UserListDto> state)
    {
        var query = state.BuildQuery(new GetUserListQuery { SearchString = this.SearchString });
        var resp = await api.GetUserList(query);

        if (resp.IsSuccessful) return resp.Content.ToGridData();

        snackbar.AddWarning(resp.ErrorMessage);
        return new GridData<UserListDto>();
    }


    public UserListDto? SelectedItem { get; private set; }
    public async Task SetSelectedItem(UserListDto item)
    {
        if (SelectedItem == item) return;
        SelectedItem = item;
        await LoadUser(item?.Id);
    }

    public string SelectedRowStyle(UserListDto item, int rowIndex)
    {
        if (item is not null && SelectedItem is not null && item.Id == SelectedItem.Id )
            return "background-color: var(--mud-palette-background-gray)";

        return "";
    }



    public UserDto? SelectedUser { get; private set; }
    private async Task LoadUser(Guid? id)
    {
        if (!id.HasValue)
        {
            SelectedUser = null;
        }
        else
        {
            var resp = await api.GetUser(id.Value);
            if (resp.IsSuccessful)
            {
                SelectedUser = resp.Content;
            }
            snackbar.AddError(resp.ErrorMessage);
        }
    }


    #region "-- Action --"

    public bool CreateDisable => true;
    public async Task Create()
    {
        if (SelectedItem != null)
        {
            snackbar.AddWarning("not implemented yet");
        }
    }

    public bool EditDisable => true;
    public async Task Edit()
    {
        if (SelectedItem != null)
        {
            snackbar.AddWarning("not implemented yet");
        }
    }

    public bool DeleteDisable => true;
    public async Task Delete()
    {
        if (SelectedItem != null)
        {
            snackbar.AddWarning("not implemented yet");
        }
    }


    public async Task AddToCommunity(CommunityListDto group)
    {
        snackbar.AddWarning("not implemented yet");
    }

    public async Task RemnoveFromCommunity(CommunityListDto group)
    {
        snackbar.AddWarning("not implemented yet");
    }

    #endregion
}
