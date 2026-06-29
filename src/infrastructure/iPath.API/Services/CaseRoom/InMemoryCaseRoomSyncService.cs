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

    public async Task<CaseRoomSnapshot> JoinAsync(Guid requestId, Guid sessionId, string? token = null, CancellationToken ct = default)
    {
        bool isGuest = false;
        Guid userId;
        string username;

        if (_userSession.User is null)
        {
            if (string.IsNullOrEmpty(token))
            {
                throw new InvalidOperationException("User not authenticated and no guest token provided.");
            }

            var isTokenValid = await _store.IsShareTokenValidAsync(requestId, token, ct);
            if (!isTokenValid)
            {
                throw new InvalidOperationException("Invalid guest token or session has no active host.");
            }

            isGuest = true;
            userId = Guid.Empty;
            username = "Guest";
        }
        else
        {
            userId = _userSession.User.Id;
            username = _userSession.User.Username ?? "User";
        }

        _joinedSessions.Add((requestId, sessionId));
        return await _store.JoinAsync(requestId, sessionId, userId, username, isGuest, ct);
    }

    public Task LeaveAsync(Guid requestId, Guid sessionId, CancellationToken ct = default)
    {
        return _store.LeaveAsync(requestId, sessionId, ct);
    }

    public Task SyncAsync(Guid requestId, SyncPayload payload, CancellationToken ct = default)
    {
        if (_userSession.User is null) return Task.CompletedTask;
        return _store.SyncAsync(requestId, payload.SessionId ?? Guid.Empty, _userSession.User.Id, payload, ct);
    }

    public Task<string> CreateShareTokenAsync(Guid requestId, CancellationToken ct = default)
    {
        if (_userSession.User is null)
            throw new InvalidOperationException("User not authenticated");
        return _store.CreateShareTokenAsync(requestId, ct);
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