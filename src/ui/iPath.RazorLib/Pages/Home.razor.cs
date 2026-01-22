using iPath.Blazor.Componenents.Shared;
using iPath.Blazor.Componenents.Users;
using Microsoft.AspNetCore.Components.Authorization;

namespace iPath.Blazor.Componenents.Pages;

public partial class Home(AppState appState, UserViewModel uvm, AuthenticationStateProvider asp, IStringLocalizer T)
{
    private UserProfile? MyProfile = null;


    private bool ShowLoginButtons = false;
    private bool ShowProfileCompletion = false;
    private bool ShowUserLinks = false;


    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);
        if (firstRender)
        {
            var state = await asp.GetAuthenticationStateAsync();
            if (state.User.Identity.IsAuthenticated)
            {
                MyProfile = await GetMyProfile();
                if (MyProfile is not null && !MyProfile.IsComplete())
                {
                    ShowProfileCompletion = true;
                }

                ShowUserLinks = true;
            }
            else
            {
                ShowLoginButtons = true;
            }
            StateHasChanged();
        }
    }

    private async Task<UserProfile?> GetMyProfile()
    {
        if (appState.User is null)
        {
            await appState.ReloadSession();
        }

        if (appState.User is not null)
        {
            return await uvm.GetProfileAsync(appState.User.Id);
        }
        return null;
    }
}