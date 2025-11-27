using iPath.Blazor.ServiceLib.ApiClient;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace iPath.Blazor.Componenents.Admin.Users;

public class UserAdminViewModel(IPathApi api, 
    ISnackbar snackbar, 
    IDialogService dialog, 
    IStringLocalizer T, 
    IMemoryCache cache,
    ILogger<UserAdminViewModel> logger) 
    : IViewModel
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

    public MudDataGrid<UserListDto> grid;


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


    const string roleListCacheKey = "admin.rolelist";
    private readonly SemaphoreSlim _cacheLock = new SemaphoreSlim(1);
    public async Task<IEnumerable<RoleDto>> GetRoles()
    {
        try
        {
            await _cacheLock.WaitAsync();
            if (!cache.TryGetValue<IEnumerable<RoleDto>>(roleListCacheKey, out var roles))
            {
                roles = await api.GetRoles();
                var opts = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromMinutes(15));
                cache.Set(roleListCacheKey, roles, opts);
            }
            return roles;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, ex.Message);
        }
        finally
        {
            _cacheLock.Release();
        }

        return null;
    }

    #region "-- Action --"

    public bool CreateDisable => false;
    public async Task Create()
    {
        DialogOptions opts = new() { MaxWidth = MaxWidth.Medium, FullWidth = false, NoHeader = false };
        var dlg = await dialog.ShowAsync<CreateUserDialog>(T["Create a new user"], options: opts);
        var res = await dlg.Result;
        var cmd = res?.Data as CreateUserCommand;
        if (cmd != null)
        {
            var resp = await api.CreateUser(cmd);
            if (!resp.IsSuccessful)
            {
                snackbar.AddWarning(resp.ErrorMessage);
            }
            await grid.ReloadServerData();
        }
    }

    public bool EditDisabled => false;
    public async Task Edit(UserListDto user)
    {
        if (user != null)
        {
            await LoadUser(user.Id);
            DialogOptions opts = new() { MaxWidth = MaxWidth.Medium, FullWidth = false, NoHeader = false };
            DialogParameters<UserAdminDialog> p = new() { { x => x.User, SelectedUser } };
            var title = string.Format(T["Admin User: {0}"], SelectedUser.Username);
            var dlg = await dialog.ShowAsync<UserAdminDialog>(title, options: opts, parameters: p);
            var res = await dlg.Result;
            if (res != null)
            {
                await grid.ReloadServerData();
            }
        }
    }

    public async Task<bool> SaveUserAccount(UpdateUserAccountCommand cmd)
    {
        if (SelectedUser != null)
        {
            var res = await api.UpdateUserAccount(cmd);
            if (res.IsSuccessful)
            {
                snackbar.AddError(res.ErrorMessage);
                return false;
            }
        }
        return true;
    }



    public async Task EditGroupMembership(UserListDto user)
    {
        if (user != null)
        {
            await LoadUser(user.Id);
            DialogOptions opts = new() { MaxWidth = MaxWidth.Large, FullWidth = true };
            var title = string.Format(T["Group Membership: {0}"], SelectedUser.Username);
            var dlg = await dialog.ShowAsync<EditUserGroupMembershipDialog>(title, opts);
            var res = await dlg.Result;
        }
    }

    public async Task SaveGroupMemberships(UpdateGroupMembershipCommand cmd)
    {
        try
        {
            var resp = await api.SetGroupMemberships(cmd);
            if (!resp.IsSuccessful)
            {
                snackbar.AddError(resp.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            snackbar.AddError(ex.Message);
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
