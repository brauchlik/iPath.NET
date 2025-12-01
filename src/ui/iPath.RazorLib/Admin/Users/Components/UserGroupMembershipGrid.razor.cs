using iPath.Blazor.Componenents.Admin.Groups;

namespace iPath.Blazor.Componenents.Admin.Users;

public partial class UserGroupMembershipGrid(GroupAdminViewModel gvm, UserAdminViewModel uvm)
{
    [Parameter]
    public Guid? selectedCommunityId { get; set; } = null;

    [Parameter]
    public bool ShowActiveOnly { get; set; } = true;


    IEnumerable<GroupListDto>? allGroups = null;
    List<GroupMemberModel>? members = null;
    List<GroupMemberModel>? displayedMembers = null;


    protected override async Task OnParametersSetAsync()
    {
        await LoadData();

        if (ShowActiveOnly)
        {
            displayedMembers = members.Where(m => m.Role != eMemberRole.None).ToList();
        }
        else
        {
            displayedMembers = members;
        }
        StateHasChanged();
    }

    protected async Task LoadData()
    {
        // get list of all groups
        if (allGroups is null)
        {
            allGroups = await gvm.GetAllAsync();

            var tmp = new List<GroupMemberModel>();
            foreach (var item in allGroups.OrderBy(c => c.Name))
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
            members = tmp;
        }
    }
}