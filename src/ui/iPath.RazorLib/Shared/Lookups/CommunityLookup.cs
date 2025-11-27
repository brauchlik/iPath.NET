using iPath.Blazor.Componenents.Communities;

namespace iPath.Blazor.Componenents.Shared.Lookups;

public class CommunityLookup(CommunityViewModel vm)
    : MudAutocomplete<CommunityListDto>
{
    [Parameter]
    public Guid? CommunityId {
        get => Value?.Id;
        set => Value = items.FirstOrDefault(c => c.Id == value); 
    }

    [Parameter]
    public EventCallback<Guid?> CommunityIdChanged { get; set; }



    private IEnumerable<CommunityListDto> items;

    protected override async Task OnInitializedAsync()
    {
        items = (await vm.GetAllAsync()).OrderBy(x => x.Name);
        await base.OnInitializedAsync();
    }

    protected override void OnInitialized()
    {  
        this.ToStringFunc = u => u is null ? "" : $"{u.Name}";
        this.SearchFunc = Search; // (string? term, CancellationToken ct) => Search(term, ct);
        base.OnInitialized();
    }

    async Task<IEnumerable<CommunityListDto>> Search(string? term, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(term))
        {
            return items.Where(x => x.Name.ToLower().Contains(term.ToLower())).ToArray();
        }
        return items;
    }
}
