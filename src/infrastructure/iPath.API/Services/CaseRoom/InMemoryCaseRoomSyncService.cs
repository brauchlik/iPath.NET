using iPath.Application.Features.CaseRoom;
using iPath.Application.Contracts;
using System.Collections.Concurrent;

namespace iPath.API.Services.CaseRoom;

public class InMemoryCaseRoomSyncService : ICaseRoomSyncService
{
    private readonly ICaseRoomSessionStore _store;
    private readonly IUserSession _userSession;
    private readonly ConcurrentBag<Guid> _joinedRequests = new();

    public InMemoryCaseRoomSyncService(
        ICaseRoomSessionStore store,
        IUserSession userSession)
    {
        _store = store;
        _userSession = userSession;
    }

    public Task<CaseRoomSnapshot> JoinAsync(Guid requestId, CancellationToken ct = default)
    {
        if (_userSession.User is null)
            throw new InvalidOperationException("User not authenticated");
        _joinedRequests.Add(requestId);
        return _store.JoinAsync(requestId, _userSession.User.Id, _userSession.User.Username, ct);
    }

    public Task LeaveAsync(Guid requestId, CancellationToken ct = default)
    {
        if (_userSession.User is null) return Task.CompletedTask;
        return _store.LeaveAsync(requestId, _userSession.User.Id, ct);
    }

    public Task SyncAsync(Guid requestId, SyncPayload payload, CancellationToken ct = default)
    {
        if (_userSession.User is null) return Task.CompletedTask;
        return _store.SyncAsync(requestId, _userSession.User.Id, payload, ct);
    }

    public async ValueTask DisposeAsync()
    {
        if (_userSession.User is not null)
        {
            foreach (var reqId in _joinedRequests)
            {
                try
                {
                    await _store.LeaveAsync(reqId, _userSession.User.Id, default);
                }
                catch { }
            }
        }
    }
}
