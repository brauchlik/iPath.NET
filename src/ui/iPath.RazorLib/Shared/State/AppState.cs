using iPath.Application.Contracts;

namespace iPath.Blazor.Componenents.Shared;

public class AppState(IPathApi api) : IUserSession
{
    private SessionUserDto _user;

    public SessionUserDto? User => _user;

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
