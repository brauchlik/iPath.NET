namespace iPath.Blazor.Componenents.Shared.Lookups;

public class CaseTypeLookup(GroupCache cache) : MudAutocomplete<string>
{
    [Parameter]
    public Guid? GroupId { get; set; }

    [Parameter]
    public EventCallback<Guid?> GroupIdChanged { get; set; }

    private IEnumerable<string> items;

    protected override void OnInitialized()
    {
        this.SearchFunc = Search; 
        base.OnInitialized();
    }

    protected override async Task OnParametersSetAsync()
    {
        if (GroupId.HasValue)
        {
            var grp = await cache.GetGroupAsync(GroupId.Value);
            if (grp is not null)
            {
                items = grp.Settings.CaseTypes;
            }
        }
    }


    async Task<IEnumerable<string>> Search(string? term, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(term))
        {
            return items.Where(x => x.ToLower().Contains(term.ToLower())).ToArray();
        }
        return items;
    }
}
