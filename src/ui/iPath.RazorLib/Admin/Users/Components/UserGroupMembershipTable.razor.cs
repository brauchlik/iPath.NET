using iPath.Blazor.Componenents.Admin.Groups;

namespace iPath.Blazor.Componenents.Admin.Users;

public partial class UserGroupMembershipTable(GroupAdminViewModel gvm, UserAdminViewModel uvm, IPathApi api)
{
    [Parameter]
    public UserDto? User { get; set; }

    [Parameter]
    public CommunityListDto? selectedCommunity { get; set; } = null;

    [Parameter]
    public bool ShowActiveOnly { get; set; } = true;

    List<GroupMemberModel>? allMemberShips = null;
    List<GroupMemberModel>? activeMemberShips = null;

    Color SaveButtonColor => allMemberShips is not null && allMemberShips.Any(m => m.HasChange) ? Color.Primary : Color.Default;

    private CommunityDto community;


    protected override async Task OnParametersSetAsync()
    {
        if (User is not null)
        {
            await LoadData();
            ApplyFilter();
        }
    }

    void ApplyFilter()
    {
        var q = allMemberShips.AsQueryable();

        if (ShowActiveOnly)
        {
            q = q.Where(m => m.Role != eMemberRole.None);
        }
        if (community != null)
        {
            q = q.Where(m => community.Groups.Any(g => g.Id == m.GroupId));
        }
        activeMemberShips = q.ToList();
        StateHasChanged();
    }

    async Task OnCommunityFilter()
    {
        if (selectedCommunity != null)
        {
            ShowActiveOnly = false;
            var resp = await api.GetCommunity(selectedCommunity.Id);
            community = resp.Content;
        }
        else
        {
            community = null;
        }
        ApplyFilter();
    }




    protected async Task LoadData()
    {
        var tmp = new List<GroupMemberModel>();
        foreach (var item in (await gvm.GetAllAsync()).OrderBy(c => c.Name))
        {
            var m = uvm.SelectedUser.GroupMembership.FirstOrDefault(m => m.GroupId == item.Id);
            if (m != null)
            {
                tmp.Add(new GroupMemberModel(m, item.Name));
            }
            else
            {
                tmp.Add(new GroupMemberModel(item.Id, item.Name));
            }
        }
        allMemberShips = tmp;
    }

    async Task Save()
    {
        if (uvm.SelectedUser is not null)
        {
            var memberships = allMemberShips.Select(m => m.ToDto()).ToArray();
            var cmd = new UpdateGroupMembershipCommand(uvm.SelectedUser.Id, memberships);
            await uvm.UpdateGroupMemberships(cmd);
            await LoadData();
        }
    }
}