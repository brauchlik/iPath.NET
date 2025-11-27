using iPath.Blazor.Componenents.Users;

namespace iPath.Blazor.Componenents.Shared.Lookups;

public class UserLookup(UserViewModel vm)
    : MudAutocomplete<UserListDto>
{
    protected override void OnInitialized()
    {
        this.ToStringFunc = u => u is null ? "" : $"{u.Username} [{u.Email}]";
        this.SearchFunc = (string? term, CancellationToken ct) => vm.Search(term, ct);
    }
}
