using iPath.Application.Contracts;
using Microsoft.AspNetCore.Components.Authorization;

namespace iPath.Blazor.Componenents.AppState;

public class AppState(IPathApi api) : IUserSession
{
    private SessionUserDto _user;

    public SessionUserDto? User => _user;

    public async Task LoadSession()
    {
        _user = SessionUserDto.Anonymous;
        //var resp = await api.GetSession();
        //if (resp.IsSuccessful)
        //{
        //    _user = resp.Content;
        //}
    }

    public async Task UnloadSession()
    {
        _user = SessionUserDto.Anonymous;
    }
}
