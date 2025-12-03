using iPath.Application.Contracts;

namespace iPath.Blazor.Componenents.Shared;

public class AppState(IPathApi api) : IUserSession
{
    private SessionUserDto _user;

    public SessionUserDto? User => _user;
    public bool IsAuthenticated => _user is not null && _user.Id != Guid.Empty;


    public async Task ReloadSession()
    {
        _user = SessionUserDto.Anonymous;
        var resp = await api.GetSession();
        if (resp.IsSuccessful)
        {
            _user = resp.Content;
        }
    }

    public void ReloadUser(Guid userId)
    {
        _user = SessionUserDto.Anonymous;
    }

    public Color PresenceColor => Color.Success;
}
