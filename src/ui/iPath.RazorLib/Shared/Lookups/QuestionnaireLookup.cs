namespace iPath.Blazor.Componenents.Shared.Lookups;

public class QuestionnaireLookup(IPathApi api)
    : MudAutocomplete<QuestionnaireListDto>
{
    private List<QuestionnaireListDto> items;

    protected override async Task OnInitializedAsync()
    {
        var resp = await api.GetQuestionnnaires(new GetQuestionnaireListQuery { PageSize = null });
        if (resp.IsSuccessful)
        {
            items = resp.Content.Items.ToList();
        }
        else
        {
            items = new();
        }
    }

    protected override void OnInitialized()
    {  
        this.ToStringFunc = u => u is null ? "" : $"{u.Name} [{u.QuestionnaireId}]";
        this.SearchFunc = Search; // (string? term, CancellationToken ct) => Search(term, ct);
        base.OnInitialized();
    }

    async Task<IEnumerable<QuestionnaireListDto>> Search(string? term, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(term))
        {
            return items.Where(x => x.QuestionnaireId.ToLower().Contains(term.ToLower()) || x.Name.ToLower().Contains(term.ToLower())).ToArray();
        }
        return items;
    }
}
