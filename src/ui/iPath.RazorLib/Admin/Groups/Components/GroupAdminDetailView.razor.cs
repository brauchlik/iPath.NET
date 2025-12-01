using Microsoft.Extensions.Localization;

namespace iPath.Blazor.Componenents.Admin.Groups;

public partial class GroupAdminDetailView(GroupAdminViewModel vm, IStringLocalizer T)
{
    [Parameter]
    public GroupDto Model { get; set; }

    IEnumerable<CommunityListDto> Communities;
    MudDataGrid<GroupMemberDto> memberGrid;


    protected override async Task OnParametersSetAsync()
    {       
        if (Model != null)
        {
            Communities = Model.Communities;
            if (memberGrid != null)
            {
                await memberGrid.ReloadServerData();
            }
        }
        StateHasChanged();
    }


    CommunityListDto communityToAdd;
    private async Task AddCommunity()
    {
        if (communityToAdd != null)
        {
            await vm.AddToCommunity(communityToAdd);
            communityToAdd = null;
            await vm.ReloadGroup();
        }
    }
    private async Task RemoveCommunity(CommunityListDto c)
    {
        await vm.RemoveFromCommunity(c);
        await vm.ReloadGroup();
    }



    OwnerDto userToAdd;
    private async Task AddUser()
    {
        if (userToAdd != null)
        {
            await vm.AddGroupMember(userToAdd);
            userToAdd = null;
            await memberGrid.ReloadServerData();
        }
    }

    private async Task RemoveUser(GroupMemberDto m)
    {
        await vm.RemoveGroupMember(m);
        await memberGrid.ReloadServerData();
    }

}