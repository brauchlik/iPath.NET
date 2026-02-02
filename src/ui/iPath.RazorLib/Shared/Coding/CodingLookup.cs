using Hl7.Fhir.Model;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.State;


namespace iPath.Blazor.Componenents.Shared.Coding;

public class CodingLookup : MudAutocomplete<CodeDisplay>
{
    private readonly IServiceProvider sp;
    public CodingLookup(IServiceProvider serviceProvider)
    {
        this.sp = serviceProvider;
        using var registerScope = CreateRegisterScope();
        _conceptState = registerScope.RegisterParameter<CodedConcept>(nameof(Concept))
            .WithParameter(() => Concept)
            .WithEventCallback(() => ConceptChanged);
    }


    [Parameter]
    public string? ValueSetId { get; set; }

    [Parameter]
    public string? CodingService { get; set; }

    [Parameter]
    public IEnumerable<CodeDisplay> Items { get; set; }



    private readonly ParameterState<CodedConcept> _conceptState; //separate field for storing parameter state

    [Parameter]
    public CodedConcept Concept { get; set; }

    [Parameter]
    public EventCallback<CodedConcept> ConceptChanged { get; set; }


    public string CodeSystemUrl { get; private set; }

    protected ValueSetDisplay vs;

    protected override async Task OnInitializedAsync()
    {
        this.ToStringFunc = x => x is null ? "" : $"{x.Display} [{x.Code}]";
        this.SearchFunc = Search; // (string? term, CancellationToken ct) => Search(term, ct);
        await base.OnInitializedAsync();

        await InitCS();
        if (Concept is not null)
        {
            SelectCode(Concept.Code);
        }


        // Subscribe to MudAutocomplete's ValueChanged and forward to ConceptChanged
        ValueChanged = EventCallback.Factory.Create<CodeDisplay>(this, async (CodeDisplay newValue) =>
        {
            // ensure internal Value is set (MudAutocomplete will normally set it for user interactions)
            Value = newValue;

            // convert to CodedConcept and notify parent
            var concept = newValue?.ToConcept(CodeSystemUrl);
            await _conceptState.SetValueAsync(concept);
        });
    }

    private bool _isReady => vs is not null;

    protected override async Task OnParametersSetAsync()
    {
        // this componenet is interactive only => no need to load data on SSR
        if (RendererInfo.IsInteractive)
        {
            await InitCS();
        }
    }

    private async Task InitCS()
    {
        if (!string.IsNullOrEmpty(ValueSetId) && !_isReady)
        {
            var srv = sp.GetKeyedService<CodingService>(CodingService);
            await srv.LoadCodeSystem();
            CodeSystemUrl = srv.CodeSystemUrl;
            await srv.LoadValueSet(ValueSetId);
            vs = srv.GetValueSetDisplay(ValueSetId);
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
            await InitCS();
            if (vs is not null)
                return await vs.FindConcepts(term, ct);
        }

        return new List<CodeDisplay>();
    }

    protected async Task SelectCode(string code)
    {
        CodeDisplay newVal = null;
        if (Items != null)
        {
            newVal = Items.FirstOrDefault(x => x.Code == code);
        }
        else if (!string.IsNullOrEmpty(ValueSetId))
        {
            if (vs is not null)
                newVal = vs.GetByCode(code);
        }
        
        await SelectOptionAsync(newVal);
    }

}