using iPath.Blazor.Componenents.Admin.Groups;

namespace iPath.Blazor.Componenents.Shared.Lookups;

public class GroupAdminLookup(GroupAdminViewModel vm)
    : MudAutocomplete<GroupListDto>
{
    [Parameter]
    public CommunityListDto? Community {  get; set; }

    protected override void OnInitialized()
    {
        this.ToStringFunc = u => u is null ? "" : u.Name;
        this.SearchFunc = (string? term, CancellationToken ct) => vm.Search(term, Community?.Id, ct);
    }
}
