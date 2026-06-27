using iPath.Application.Features.CaseRoom;
using iPath.Application.Contracts;
using System.Collections.Concurrent;

namespace iPath.API.Services.CaseRoom;

public class InMemoryCaseRoomSyncService : ICaseRoomSyncService
{
    private readonly ICaseRoomSessionStore _store;
    private readonly IUserSession _userSession;
    private readonly ConcurrentBag<(Guid RequestId, Guid SessionId)> _joinedSessions = new();

    public InMemoryCaseRoomSyncService(
        ICaseRoomSessionStore store,
        IUserSession userSession)
    {
        _store = store;
        _userSession = userSession;
    }

    public Task<CaseRoomSnapshot> JoinAsync(Guid requestId, Guid sessionId, CancellationToken ct = default)
    {
        if (_userSession.User is null)
            throw new InvalidOperationException("User not authenticated");
        _joinedSessions.Add((requestId, sessionId));
        return _store.JoinAsync(requestId, sessionId, _userSession.User.Id, _userSession.User.Username, ct);
    }

    public Task LeaveAsync(Guid requestId, Guid sessionId, CancellationToken ct = default)
    {
        if (_userSession.User is null) return Task.CompletedTask;
        return _store.LeaveAsync(requestId, sessionId, ct);
    }

    public Task SyncAsync(Guid requestId, SyncPayload payload, CancellationToken ct = default)
    {
        if (_userSession.User is null) return Task.CompletedTask;
        return _store.SyncAsync(requestId, payload.SessionId ?? Guid.Empty, _userSession.User.Id, payload, ct);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var (reqId, sessId) in _joinedSessions)
        {
            try
            {
                await _store.LeaveAsync(reqId, sessId, default);
            }
            catch { }
        }
    }
}