using iPath.Blazor.ServiceLib.ApiClient;
using iPath.Domain.Entities;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace iPath.Blazor.Componenents.Admin.Groups;

public class GroupAdminViewModel(IPathApi api, 
    ISnackbar snackbar,
    IDialogService dialog,
    IMemoryCache cache,
    IStringLocalizer T,
    ILogger<GroupAdminViewModel> logger)
    : IViewModel
{    
    public string SearchString { get; set; } = "";
    public MudDataGrid<GroupListDto> grid;

    public async Task<GridData<GroupListDto>> GetListAsync(GridState<GroupListDto> state)
    {
        var query = state.BuildQuery(new GetGroupListQuery { AdminList = true, SearchString = this.SearchString });
        var resp = await api.GetGroupList(query);

        if (resp.IsSuccessful) return resp.Content.ToGridData();

        snackbar.AddWarning(resp.ErrorMessage);
        return new GridData<GroupListDto>();
    }



    const string groupListCacheKey = "admin.grouplist";
    private readonly SemaphoreSlim _cacheLock = new SemaphoreSlim(1);

    public async Task<IEnumerable<GroupListDto>> GetAllAsync()
    {
        try
        {
            await _cacheLock.WaitAsync();
            if (!cache.TryGetValue<IEnumerable<GroupListDto>>(groupListCacheKey, out var grouplist))
            {
                var query = new GetGroupListQuery { PageSize = null, AdminList = true };
                var resp = await api.GetGroupList(query);
                if (resp.IsSuccessful)
                {
                    grouplist = resp.Content.Items.OrderBy(g => g.Name);

                    var opts = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromMinutes(15));
                    cache.Set(groupListCacheKey, grouplist, opts);
                }
            }
            return grouplist;
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

    private void DeleteGroupListCache()
    {
        cache.Remove(groupListCacheKey);
    }

    public async Task<IEnumerable<GroupListDto>> Search(string? term, Guid? communityId, CancellationToken ct)
    {
        var list = await GetAllAsync();
        if (string.IsNullOrEmpty(term))
        {
            return list;
        }
        else
        {
            return list.Where(g => g.Name.ToLower().StartsWith(term.ToLower()));
        }
    }




    public GroupListDto SelectedItem { get; private set; }
    public async Task SetSelectedItem(GroupListDto item)
    {
        if (SelectedItem == item) return;
        SelectedItem = item;
        await LoadGroup(item?.Id);
    }

    public string SelectedRowStyle(GroupListDto item, int rowIndex)
    {
        if (item is not null && SelectedItem is not null && item.Id == SelectedItem.Id )
            return "background-color: var(--mud-palette-background-gray)";

        return "";
    }



    public GroupDto SelectedGroup { get; private set; }
    private async Task LoadGroup(Guid? id)
    {
        if (!id.HasValue)
        {
            SelectedGroup = null;
        }
        else
        {
            var resp = await api.GetGroup(id.Value);
            if (resp.IsSuccessful)
            {
                SelectedGroup = resp.Content;
            }
            snackbar.AddError(resp.ErrorMessage);
        }
    }


    public async Task Create()
    {
        DialogOptions opts = new() { MaxWidth = MaxWidth.Medium, FullWidth = false, NoHeader = false };
        var dlg = await dialog.ShowAsync<CreateGroupDialog>(T["Create a new group"], options: opts);
        var res = await dlg.Result;
        var cmd = res?.Data as CreateGroupCommand;
        if (cmd != null)
        {
            var resp = await api.CreateGroup(cmd);
            if (!resp.IsSuccessful)
            {
                snackbar.AddWarning(resp.ErrorMessage);
            }
            await grid.ReloadServerData();
        }
    }


    public async Task Edit()
    {
        if (SelectedGroup != null)
        {
            var m = new GroupEditModel
            {
                Id = SelectedGroup.Id,
                Name = SelectedGroup.Name,
                Settings = SelectedGroup.Settings,
                Owner = SelectedGroup.Owner
            };

            var p = new DialogParameters<EditGroupDialog> { { x => x.Model, m } };
            DialogOptions opts = new() { MaxWidth = MaxWidth.Medium, FullWidth = false, NoHeader = false };
            var dlg = await dialog.ShowAsync<EditGroupDialog>(T["Edit group"], options: opts, parameters: p);
            var res = await dlg.Result;
            var r = res?.Data as GroupEditModel;
            if (r != null && r.Id.HasValue)
            {
                var cmd = new UpdateGroupCommand
                {
                    Id = r.Id.Value,
                    Name = r.Name,
                    OwnerId = r.Owner.Id,
                    Settings = r.Settings
                };
                var resp = await api.UpdateGroup(cmd);
                if (!resp.IsSuccessful)
                {
                    snackbar.AddWarning(resp.ErrorMessage);
                }
                await grid.ReloadServerData();
            }
        }
    }

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
}


public class GroupEditModel
{
    public Guid? Id { get; set; }
    [Required]
    public string Name { get; set; } = "";
    public GroupSettings Settings { get; set; } = new();
    public OwnerDto? Owner { get; set; }
    public CommunityListDto? Community { get; set; }
}


public class CreateGroupCommandModel : CreateGroupCommand
{
    public OwnerDto Owner { 
        get; 
        set
        {
            field = value;
            if (value is not null)
            {
                OwnerId = value.Id;
            }
        } 
    }
    public CommunityListDto? Community {
        get; 
        set
        {
            field = value;
            CommunityId = value?.Id;
        }
    }

    //public CreateGroupCommand ToCommand()
    //{
    //    OwnerId = Owner.Id;
    //    CommunityId = Community?.Id;
    //    return (CreateGroupCommand)this.MemberwiseClone();
    //}
}
