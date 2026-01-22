using Microsoft.Extensions.DependencyInjection;

namespace iPath.Blazor.Componenents.Shared.Coding;

public class CodingLookup(IServiceProvider sp) : MudAutocomplete<CodeDisplay>
{
    [Parameter]
    public string ValueSetId { get; set; }

    [Parameter]
    public string CodingService { get; set; } = "";

    [Parameter]
    public IEnumerable<CodeDisplay> Items { get; set; }



    // Data-Binding to CodedConcept
    [Parameter]
    public CodedConcept Concept
    {
        get => Value.ToConcept(CodeSystemUrl);
        set
        {
            field = value;
            SelectCode(field?.Code);
        }
    }

    [Parameter]
    public EventCallback<CodedConcept> ConceptChanged { get; set; }



    public string CodeSystemUrl => srv.CodeSystemUrl;

    protected CodingService srv;

    protected override void OnInitialized()
    {
        srv = sp.GetKeyedService<CodingService>(CodingService);

        this.ToStringFunc = x => x is null ? "" : $"{x.Display} [{x.Code}]";
        this.SearchFunc = Search; // (string? term, CancellationToken ct) => Search(term, ct);
        base.OnInitialized();

        // Subscribe to MudAutocomplete's ValueChanged and forward to ConceptChanged
        ValueChanged = EventCallback.Factory.Create<CodeDisplay>(this, async (CodeDisplay newValue) =>
        {
            // ensure internal Value is set (MudAutocomplete will normally set it for user interactions)
            Value = newValue;

            // convert to CodedConcept and notify parent
            var concept = newValue?.ToConcept(CodeSystemUrl);
            await ConceptChanged.InvokeAsync(concept);
        });
    }

    private bool _isReady = false;
    protected override async Task OnParametersSetAsync()
    {
        // this componenet is interactive only => no need to load data on SSR
        if (RendererInfo.IsInteractive)
        {
            if (!string.IsNullOrEmpty(ValueSetId) && !_isReady)
            {
                await srv.LoadCodeSystem();
                await srv.LoadValueSet(ValueSetId);
                _isReady = true;
            }
        }
    }

    async Task<IEnumerable<CodeDisplay>> Search(string? term, CancellationToken ct)
    {
        if (Items != null)
        {
            return Items.FindConcepts(term);
        }
        else if (!string.IsNullOrEmpty(ValueSetId))
        {
            var vs = srv.GetValueSetDisplay(ValueSetId);
            if (vs is not null)
                return await vs.FindConcepts(term, ct);
        }

        return new List<CodeDisplay>();
    }

    protected void SelectCode(string code)
    {
        if (Items != null)
        {
            Value = Items.FirstOrDefault(x => x.Code == code);
        }
        else if (!string.IsNullOrEmpty(ValueSetId))
        {
            var vs = srv.GetValueSetDisplay(ValueSetId);
            if (vs is not null) 
                Value = vs.FindConceptByCode(code);
        }
    }


}