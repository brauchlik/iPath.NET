using iPath.Blazor.Componenents.Users;

namespace iPath.Blazor.Componenents.Shared.Lookups;

public class OwnerLookup(UserViewModel vm)
    : MudAutocomplete<OwnerDto>
{
    protected override void OnInitialized()
    {
        this.ToStringFunc = u => u is null ? "" : u.Username;
        this.SearchFunc = (string? term, CancellationToken ct) => vm.SearchOwners(term, ct);
    }
}