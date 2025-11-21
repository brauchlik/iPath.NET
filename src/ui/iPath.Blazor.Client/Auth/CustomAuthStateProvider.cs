using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;

namespace iPath.Blazor.Client.Auth;

/*
public class CustomAuthStateProvider : AuthenticationStateProvider
{
    private readonly RemoteAuthenticationService<RemoteUserAccount> _inner;

    public CustomAuthStateProvider(RemoteAuthenticationService<RemoteUserAccount> inner)
    {
        _inner = inner;
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
        => _inner.GetAuthenticationStateAsync();

    public void NotifyUserChanged()
        => NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
}
*/