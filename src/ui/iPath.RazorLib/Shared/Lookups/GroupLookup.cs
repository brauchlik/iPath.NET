using iPath.Blazor.Componenents.Groups;

namespace iPath.Blazor.Componenents.Shared.Lookups;

public class GroupLookup(GroupListViewModel vm)
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
