using iPath.Blazor.Componenents.Admin.Users;
using Microsoft.Extensions.Caching.Memory;

namespace iPath.Blazor.Componenents.Admin.Groups;

public class GroupAdminViewModel(IPathApi api,
    ISnackbar snackbar,
    IDialogService dialog,
    IMemoryCache memCache,
    GroupCache grpCache,
    IStringLocalizer T,
    ILogger<GroupAdminViewModel> logger)
    : IViewModel
{

    public Action OnChange { get; set; }


    public List<BreadcrumbItem> BreadCrumbs
    {
        get
        {
            var ret = new List<BreadcrumbItem> { new(T["Administration"], href: "admin") };
            if (SelectedGroup is null)
            {
                ret.Add(new(T["Groups"], href: null, disabled: true));
            }
            else
            {
                ret.Add(new(T["Groups"], href: "admin/groups"));
                ret.Add(new(SelectedGroup.Name, href: null, disabled: true));
            }
            return ret;
        }
    }



    public string SearchString { get; set; } = "";
    public MudDataGrid<GroupListDto> grid;

    public async Task<GridData<GroupListDto>> GetListAsync(GridState<GroupListDto> state, CancellationToken ct = default)
    {
        var query = state.BuildQuery(new GetGroupListQuery { AdminList = true, SearchString = this.SearchString });
        var resp = await api.GetGroupList(query);

        if (resp.IsSuccessful) return resp.Content.ToGridData();

        snackbar.AddWarning(resp.ErrorMessage);
        return new GridData<GroupListDto>();
    }


    public MudTable<GroupListDto> table;

    public async Task<TableData<GroupListDto>> GetTableAsync(TableState state, CancellationToken ct = default)
    {
        var query = state.BuildQuery(new GetGroupListQuery { AdminList = true, SearchString = this.SearchString });
        var resp = await api.GetGroupList(query);

        if (resp.IsSuccessful) return resp.Content.ToTableData();

        snackbar.AddWarning(resp.ErrorMessage);
        return new TableData<GroupListDto>();
    }



    const string groupListCacheKey = "admin.grouplist";
    private readonly SemaphoreSlim _cacheLock = new SemaphoreSlim(1);

    public async Task<IEnumerable<GroupListDto>> GetAllAsync()
    {
        try
        {
            await _cacheLock.WaitAsync();
            if (!memCache.TryGetValue<IEnumerable<GroupListDto>>(groupListCacheKey, out var grouplist))
            {
                var query = new GetGroupListQuery { PageSize = null, AdminList = true };
                var resp = await api.GetGroupList(query);
                if (resp.IsSuccessful)
                {
                    grouplist = resp.Content.Items.OrderBy(g => g.Name);

                    var opts = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromMinutes(15));
                    memCache.Set(groupListCacheKey, grouplist, opts);
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
        memCache.Remove(groupListCacheKey);
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
        if (item is not null && SelectedItem is not null && item.Id == SelectedItem.Id)
            return "background-color: var(--mud-palette-background-gray)";

        return "";
    }



    public GroupDto? SelectedGroup { get; private set; }
    public bool IsLoading;
    public async Task LoadGroup(Guid? id)
    {
        IsLoading = true;
        SelectedGroup = null;
        if (id.HasValue)
        {
            try
            {
                var resp = await api.GetGroup(id.Value);
                if (resp.IsSuccessful)
                {
                    SelectedGroup = resp.Content;
                }
                else
                {
                    snackbar.AddError(resp.ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                snackbar.AddError(ex.Message);
            }
        }

        IsLoading = false;

        OnChange?.Invoke();
    }

    public async Task<GroupDto?> ReloadGroup()
    {
        if (SelectedGroup != null)
        {
            await LoadGroup(SelectedGroup.Id);
        }
        return SelectedGroup;
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
            if (grid is not null) await grid.ReloadServerData();
            if (table is not null) await table.ReloadServerData();
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
                Owner = SelectedGroup.Owner,
                Community = SelectedGroup.Community,
            };

            var p = new DialogParameters<EditGroupDialog> { { x => x.Model, m } };
            DialogOptions opts = new() { MaxWidth = MaxWidth.Medium, FullWidth = false, NoHeader = false };
            var dlg = await dialog.ShowAsync<EditGroupDialog>(T["Edit group"], options: opts, parameters: p);
            var res = await dlg.Result;
            var r = res?.Data as GroupEditModel;
            if (r != null && r.Id.HasValue)
            {
                if (await SaveEditModel(r))
                {
                    await grid.ReloadServerData();
                }
            }
        }
    }

    public async Task<bool> SaveEditModel(GroupEditModel editModel)
    {
        var cmd = new UpdateGroupCommand
        {
            Id = editModel.Id.Value,
            Name = editModel.Name,
            OwnerId = editModel.Owner?.Id,
            CommunityId = editModel.Community?.Id,
            Settings = editModel.Settings
        };
        var resp = await api.UpdateGroup(cmd);
        if (!resp.IsSuccessful)
        {
            snackbar.AddWarning(resp.ErrorMessage);
            return false;
        }

        await grpCache.ClearGroup(cmd.Id);
        return true;
    }


    public async Task Delete()
    {
        if (SelectedItem != null)
        {
            snackbar.AddWarning("not implemented yet");
        }
    }


    public async Task AddToCommunity(CommunityListDto community) => ToggleCommunity(community, false);
    public async Task RemoveFromCommunity(CommunityListDto community) => ToggleCommunity(community, true);

    public async Task ToggleCommunity(CommunityListDto community, bool remove)
    {
        if (SelectedGroup != null)
        {
            var cmd = new AssignGroupToCommunityCommand(GroupId: SelectedGroup.Id, CommunityId: community.Id, Remove: remove);
            var resp = await api.AssignGroupToCommunity(cmd);
            if (!resp.IsSuccessful)
            {
                snackbar.AddError(resp.ErrorMessage);
            }
        }
    }


    public string MemberSearchString { get; set; }

    public async Task<GridData<GroupMemberDto>> GetMembersGridAsync(GridState<GroupMemberDto> state, CancellationToken ct = default)
    {
        if (SelectedGroup is not null)
        {
            var query = state.BuildQuery(new GetGroupMembersQuery { GroupId = SelectedGroup.Id, SearchString = this.MemberSearchString });
            var resp = await api.GetGrouMembers(query);

            if (resp.IsSuccessful) return resp.Content.ToGridData();

            snackbar.AddWarning(resp.ErrorMessage);
        }
        return new GridData<GroupMemberDto>();
    }

    public async Task<List<GroupMemberModel>> GetMembersAsync(CancellationToken ct = default)
    {
        if (SelectedGroup is not null)
        {
            var query = new GetGroupMembersQuery { GroupId = SelectedGroup.Id, PageSize = null };
            var resp = await api.GetGrouMembers(query);

            if (resp.IsSuccessful)
            {
                return resp.Content.Items.Select(m => new GroupMemberModel(m, SelectedGroup.Id, SelectedGroup.Name)).ToList();
            }
            snackbar.AddWarning(resp.ErrorMessage);
        }
        return new List<GroupMemberModel>();
    }


    public async Task<GroupMemberModel?> AddGroupMember(OwnerDto owner)
    {
        if (SelectedGroup is not null)
        {
            var cmd = new AssignUserToGroupCommand(userId: owner.Id, groupId: SelectedGroup.Id, role: eMemberRole.User, isConsultant: false);
            var resp = await api.AssignUserToGroup(cmd);
            if (!resp.IsSuccessful)
            {
                snackbar.AddError(resp.ErrorMessage);
            }
            else
            {
                return new GroupMemberModel(resp.Content, SelectedGroup.Id, SelectedGroup.Name);
            }
        }
        return null;
    }

    public async Task RemoveGroupMember(GroupMemberDto member)
    {
        if (SelectedGroup is not null)
        {
            UserGroupMemberDto[] list = {
                new UserGroupMemberDto(GroupId: SelectedGroup.Id, Groupname: "", Role: eMemberRole.None, IsConsultant: false)
            };
            var cmd = new UpdateGroupMembershipCommand(member.UserId, list);
            var resp = await api.UpdateGroupMemberships(cmd);
            if (!resp.IsSuccessful)
                snackbar.AddError(resp.ErrorMessage);
        }
    }

    public async Task UpdateMember(GroupMemberModel m)
    {
        if (m.Role == eMemberRole.None)
        {
            await RemoveGroupMember(m.ToGroupMemberDto());
        }
        else
        {
            var cmd = new AssignUserToGroupCommand(groupId: m.GroupId, userId: m.UserId, role: m.Role, isConsultant: m.IsConsultant);

            var resp = await api.AssignUserToGroup(cmd);
            if (!resp.IsSuccessful)
                snackbar.AddError(resp.ErrorMessage);
        }
        OnChange?.Invoke();
    }



    public async Task EditQuestionnaires()
    {
        if (SelectedGroup != null)
        {
            var p = new DialogParameters<EditGroupQuestionnairesDialog> { { x => x.Model, SelectedGroup } };
            var dlg = await dialog.ShowAsync<EditGroupQuestionnairesDialog>("...", parameters: p);
            var res = await dlg.Result;
        }
    }


    public async Task SaveQuestionnaireUsage(GroupQuestionnareModel model, eQuestionnaireUsage change)
    {
        try
        {
            bool remove = !model.Usage[change];
            var cmd = new AssignQuestionnaireCommand(model.QuestionnaireId, change, remove, GroupId: model.GroupId);
            var resp = await api.AssignQuestionnaire(cmd);
            if (resp.IsSuccessful) return;

            snackbar.AddError(resp.ErrorMessage);
        }
        catch (Exception ex)
        {
            snackbar.AddError(ex.Message);
        }

        // on error => reset Usage
        model.Usage[change] = !model.Usage[change];
    }
}


public class CreateGroupCommandModel : CreateGroupCommand
{
    public OwnerDto Owner
    {
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
    public CommunityListDto Community
    {
        get;
        set
        {
            field = value;
            CommunityId = value.Id;
        }
    }

    //public CreateGroupCommand ToCommand()
    //{
    //    OwnerId = Owner.Id;
    //    CommunityId = Community?.Id;
    //    return (CreateGroupCommand)this.MemberwiseClone();
    //}
}



public class GroupQuestionnareModel
{
    public Guid QuestionnaireId { get; init; }

    public string Id { get; init; }
    public string Name { get; init; }
    public string Filter { get; init; } = "";

    public string NameAndId => $"{Name} [{Id}]";

    public Guid GroupId { get; init; }

    public Dictionary<eQuestionnaireUsage, bool> Usage = new();

    public GroupQuestionnareModel(Guid QuestionnaireId, string Id, string Name, string? Filter, Guid GroupId)
    {
        this.QuestionnaireId = QuestionnaireId;
        this.Id = Id;
        this.Name = Name;
        this.GroupId = GroupId;
        this.Filter = Filter ?? "";

        foreach (var e in QuestionnairesViewModel.Usages)
        {
            Usage.Add((eQuestionnaireUsage)e, false);
        }
    }
}