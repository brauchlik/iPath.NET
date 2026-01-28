using FluentResults;
using iPath.Blazor.Componenents.Users;
using Microsoft.Extensions.Caching.Memory;
using Refit;
using System.Runtime.CompilerServices;

namespace iPath.Blazor.Componenents.Admin.Users;

public class UserAdminViewModel(IPathApi api,
    ISnackbar snackbar,
    IDialogService dialog,
    IStringLocalizer T,
    UserViewModel userVM,
    IMemoryCache cache,
    ILogger<UserAdminViewModel> logger)
    : IViewModel
{


    public List<BreadcrumbItem> BreadCrumbs
    {
        get
        {
            var ret = new List<BreadcrumbItem> { new(T["Administration"], href: "admin") };
            if (SelectedUser is null)
            {
                ret.Add(new(T["User"], href: null, disabled: true));
            }
            else
            {
                ret.Add(new(T["User"], href: "admin/users"));
                ret.Add(new(SelectedUser.Username, href: null, disabled: true));
            }
            return ret;
        }
    }

    public string SearchString { get; set; } = "";


    public MudDataGrid<UserListDto> grid;
    public async Task<GridData<UserListDto>> GetListAsync(GridState<UserListDto> state, CancellationToken ct = default)
    {
        var query = state.BuildQuery(new GetUserListQuery { SearchString = this.SearchString });
        var resp = await api.GetUserList(query);

        if (resp.IsSuccessful) return resp.Content.ToGridData();

        snackbar.AddWarning(resp.ErrorMessage);
        return new GridData<UserListDto>();
    }



    public MudTable<UserListDto> table;
    public async Task<TableData<UserListDto>> GetTableDataAsync(TableState state, CancellationToken ct)
    {
        var query = state.BuildQuery(new GetUserListQuery { SearchString = this.SearchString });
        var resp = await api.GetUserList(query);

        if (resp.IsSuccessful) return resp.Content.ToTableData();

        snackbar.AddWarning(resp.ErrorMessage);
        return new TableData<UserListDto>();
    }

    async Task ReloadServerData()
    {
        if (grid is not null)
            await grid.ReloadServerData();
        if (table is not null)
            await table.ReloadServerData();
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
        if (item is not null && SelectedItem is not null && item.Id == SelectedItem.Id)
            return "background-color: var(--mud-palette-background-gray)";

        return "";
    }



    public UserDto? SelectedUser { get; private set; }
    public async Task LoadUser(Guid? id)
    {
        if (!id.HasValue)
        {
            SelectedUser = null;
        }
        else
        {
            await ProcessUserDtoQuery(api.GetUser(id.Value));
        }
    }

    async Task ProcessUserDtoQuery(Task<IApiResponse<UserDto>> query, [CallerMemberName] string? caller = default)
    {
        try
        {
            var resp = await query;
            if (!resp.IsSuccessful)
            {
                snackbar.AddWarning(resp.ErrorMessage);
            }
            else
            {
                SelectedUser = resp.Content;
            }
        }
        catch (Exception ex)
        {
            logger.LogError($"{0} in {1}", ex.Message, caller, ex);
            snackbar.AddError(ex.Message);
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
                var resp = await api.GetRoles();
                if (resp.IsSuccessful)
                {
                    roles = resp.Content;
                    var opts = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromMinutes(15));
                    cache.Set(roleListCacheKey, roles, opts);
                }
                else
                {
                    logger.LogError("Error in GetRoles", resp.ErrorMessage);
                    snackbar.AddError(resp.ErrorMessage);
                }
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
            await ReloadServerData();
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
                await ReloadServerData();
            }
        }
    }

    public async Task<bool> SaveUserAccount(UpdateUserAccountCommand cmd)
    {
        try
        {
            var res = await api.UpdateUserAccount(cmd);
            if (res.IsSuccessful)
            {
                if (SelectedUser is not null) await LoadUser(SelectedUser.Id);
                return true;
            }

            snackbar.AddError(res.ErrorMessage);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, ex.Message);
            snackbar.AddError(ex.Message);
        }

        userVM.ClearProfileCache(cmd.UserId);

        return false;
    }

    public async Task ResetPassword()
    {
        var p = new DialogParameters<UserPasswordDialog> { { x => x.User, SelectedUser } };
        var d = await dialog.ShowAsync<UserPasswordDialog>("Reset Password", parameters: p);
        await d.Result;
    }

    public async Task<Result> UpdateUserPassword(UpdateUserPasswordCommand cmd)
    {
        var resp = await api.UpdateUserPassword(cmd);
        if (resp.IsSuccessful)
        {
            if (resp.Content.IsSuccess)
            {
                snackbar.AddInfo("Password has been reset");
            }
            return resp.Content;
        }
        return Result.Fail(resp.ErrorMessage);
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

    public async Task UpdateGroupMemberships(UpdateGroupMembershipCommand cmd)
        => await ProcessUserDtoQuery(api.UpdateGroupMemberships(cmd));


    public bool DeleteDisable => true;
    public async Task Delete(UserListDto user)
    {
        if (user != null)
        {
            var res = await dialog.ShowMessageBoxAsync("Warning",
                $"Are you sure that you want to delete user {user.Username} completely?",
                yesText: "Yes", cancelText: "Cancel");
            if (res is not null)
            {
                var resp = await api.DeleteUser(user.Id);
                if (resp.IsSuccessful)
                {
                    await table.ReloadServerData();
                }
                else
                {
                    snackbar.AddError(resp.ErrorMessage);
                }
            }
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

    public async Task UpdateCommunityMemberships(UpdateCommunityMembershipCommand cmd)
        => await ProcessUserDtoQuery(api.UpdateCommunityMemberships(cmd));


    public async Task UpdateNotifications(UserNotificationModel model)
        => await UpdateNotifications(new UpdateUserNotificationsCommand(UserId: model.UserId, Notifications: new[] { model.ToDto() } ));
        
    public async Task UpdateNotifications(UpdateUserNotificationsCommand cmd)
        => await ProcessUserDtoQuery(api.UpdateUserNotification(cmd));

    #endregion
}
