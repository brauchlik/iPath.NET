using iPath.Blazor.ServiceLib.ApiClient;

namespace iPath.Blazor.Componenents.Shared.Lookups;

public class ConsultantLookup(IPathApi api)
    : MudAutocomplete<ConsultantDto>
{
    [Parameter]
    public Guid? GroupId { get; set; }

    [Parameter]
    public Guid? CommunityId { get; set; }

    [Parameter]
    public string? BodySiteCode { get; set; }

    protected override void OnInitialized()
    {
        this.ToStringFunc = c => c is null ? "" : c.ToDisplay();
        this.SearchFunc = Search;
    }

    private async Task<IEnumerable<ConsultantDto>> Search(string? term, CancellationToken ct)
    {
        var query = new GetConsultantsQuery
        {
            SearchString = term,
            GroupId = GroupId,
            CommunityId = CommunityId,
            BodySiteCode = BodySiteCode,
            Page = 0,
            PageSize = 100
        };
        var resp = await api.GetConsultants(query);
        if (resp.IsSuccessful)
            return resp.Content.Items;
        return [];
    }
}
