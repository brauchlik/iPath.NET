using iPath.Blazor.Componenents.Groups;

namespace iPath.Blazor.Componenents.Shared.Lookups;

public class GroupLookup
    : MudAutocomplete<GroupListDto>
{
    [Parameter]
    public CommunityListDto? Community {  get; set; }

    // Das neue Binding für die Id
    [Parameter]
    public Guid? SelectedGroupId { get; set; }

    [Parameter]
    public EventCallback<Guid?> SelectedGroupIdChanged { get; set; }


    private readonly GroupListViewModel vm;
    public GroupLookup(GroupListViewModel _vm)
    {
        vm = _vm;

        // binde an das interne ValueChanged von MudAutocomplete
        ValueChanged = Microsoft.AspNetCore.Components.EventCallback.Factory.Create<GroupListDto>(this, OnInternalValueChanged);
    }

    protected override void OnInitialized()
    {
        this.ToStringFunc = u => u is null ? "" : u.Name;
        this.SearchFunc = (string? term, CancellationToken ct) => vm.Search(term, Community?.Id, ct);
    }

    private async Task OnInternalValueChanged(GroupListDto? selectedGroup)
    {
        // 1. Das normale Objekt-Binding bedienen (falls @bind-Value genutzt wird)
        Value = selectedGroup;

        // 2. Die ID extrahieren und per Callback nach aussen geben
        var newId = selectedGroup?.Id;
        if (SelectedGroupId != newId)
        {
            SelectedGroupId = newId;
            await SelectedGroupIdChanged.InvokeAsync(newId);
        }
    }

}
