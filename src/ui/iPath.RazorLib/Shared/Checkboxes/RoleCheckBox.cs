using iPath.Blazor.Componenents.Admin.Users;

namespace iPath.Blazor.Componenents.Shared.Checkboxes;


/*
 * DO NOT USE yet
 * A checkbox for selecting a specific role for a group member.
*/



public class RoleCheckBox : MudCheckBox<GroupMemberModel>
{
    [Parameter]
    public eMemberRole Role { get; set; }


    protected override void OnInitialized()
    {
        base.OnInitialized();
        StopClickPropagation = true;
    }



}
