using iPath.Domain.Entities.Base;
using Microsoft.Extensions.DependencyInjection;

namespace iPath.Blazor.Componenents.Shared.Coding;

public partial class CodingFilterView(IServiceProvider sp)
{
    [Parameter, EditorRequired]
    public ConceptFilter Filter { get; set; }

    [Parameter, EditorRequired]
    public string CodingService { get; set; }

    [Parameter, EditorRequired]
    public string ValueSetId { get; set; }


    protected CodedConcept? NewValue {  get; set; }

    CodingLookup lookup;

    protected override void OnInitialized()
    {
        Filter = new ConceptFilter();
        Filter.Concetps.Add(new CodedConcept { System = CodedConcept.IcodUrl, Code = "C10.3", Display = "Test 1" });
        Filter.Concetps.Add(new CodedConcept { System = CodedConcept.IcodUrl, Code = "C41.3", Display = "Test 2" });
        Filter.Concetps.Add(new CodedConcept { System = CodedConcept.IcodUrl, Code = "C73.0", Display = "Test 3" });
    }


    async Task AddCode()
    {
        if (NewValue is not null)
        {
            Filter.Add(NewValue);
            StateHasChanged();
            await lookup.ClearAsync();
        }
    }

    void Remove(CodedConcept c)
    {
        Filter.Remove(c);
    }
}
