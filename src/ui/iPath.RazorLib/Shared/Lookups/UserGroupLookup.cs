using iPath.Blazor.Componenents.Users;

namespace iPath.Blazor.Componenents.Shared.Lookups;

public class UserGroupLookup
    : MudAutocomplete<UserGroupMemberDto>
{
    [Parameter, EditorRequired]
    public Guid UserId { get; set; }

    // Das neue Binding für die Id
    [Parameter]
    public Guid? SelectedGroupId { get; set; }

    [Parameter]
    public EventCallback<Guid?> SelectedGroupIdChanged { get; set; }


    private readonly UserViewModel vm;
    public UserGroupLookup(UserViewModel _vm)
    {
        vm = _vm;

        // binde an das interne ValueChanged von MudAutocomplete
        ValueChanged = Microsoft.AspNetCore.Components.EventCallback.Factory.Create<UserGroupMemberDto>(this, OnInternalValueChanged);
    }


    private UserDto user;
    protected override async Task OnInitializedAsync()
    {
        // 1. Erst Daten laden
        user = await vm.GetUserAsync(UserId);

        // 2. Konfiguration setzen
        this.ToStringFunc = u => u?.Groupname ?? "";
        this.SearchFunc = SearchInList;

        // 3. Initialen Wert setzen (falls user schon da)
        SyncValueFromId();
    }

    // Wichtig für Updates von aussen (z.B. wenn die ID via Code geändert wird)
    protected override void OnParametersSet()
    {
        base.OnParametersSet();
        SyncValueFromId();
    }

    private void SyncValueFromId()
    {
        if (user is not null && SelectedGroupId.HasValue)
        {
            var matchedGroup = user.GroupMembership.FirstOrDefault(m => m.GroupId == SelectedGroupId);

            // WICHTIG: Nur zuweisen, wenn sich der Wert wirklich unterscheidet
            if (matchedGroup != null && Value != matchedGroup)
            {
                Value = matchedGroup;
                // Falls das Feld leer bleibt, setzen wir den Text manuell:
                Text = ToStringFunc(matchedGroup);
            }
        }
    }


    private Task<IEnumerable<UserGroupMemberDto>> SearchInList(string term, CancellationToken ct)
    {
        if (user is null) return Task.FromResult((new List<UserGroupMemberDto>()).AsEnumerable());

        // Wenn kein Suchbegriff da ist, zeige alles (oder nichts)
        if (string.IsNullOrEmpty(term))
            return Task.FromResult(user.GroupMembership.AsEnumerable());

        // Lokale Suche (Case Insensitive)
        var result = user.GroupMembership.Where(x => x.Groupname.Contains(term, StringComparison.OrdinalIgnoreCase));

        return Task.FromResult(result);
    }

    private async Task OnInternalValueChanged(UserGroupMemberDto? selectedGroup)
    {
        // 1. Das normale Objekt-Binding bedienen (falls @bind-Value genutzt wird)
        Value = selectedGroup;

        // 2. Die ID extrahieren und per Callback nach aussen geben
        var newId = selectedGroup?.GroupId;
        if (SelectedGroupId != newId)
        {
            SelectedGroupId = newId;
            await SelectedGroupIdChanged.InvokeAsync(newId);
        }
    }
}