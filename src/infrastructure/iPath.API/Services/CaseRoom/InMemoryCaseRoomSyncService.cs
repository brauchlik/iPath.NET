using iPath.Application.Features.CaseRoom;
using iPath.Application.Contracts;

namespace iPath.API.Services.CaseRoom;

public class InMemoryCaseRoomSyncService(
    ICaseRoomSessionStore store,
    IUserSession userSession) : ICaseRoomSyncService
{
    public Task<CaseRoomSnapshot> JoinAsync(Guid requestId, CancellationToken ct = default)
    {
        if (userSession.User is null)
            throw new InvalidOperationException("User not authenticated");
        return store.JoinAsync(requestId, userSession.User.Id, userSession.User.Username, ct);
    }

    public Task LeaveAsync(Guid requestId, CancellationToken ct = default)
    {
        if (userSession.User is null) return Task.CompletedTask;
        return store.LeaveAsync(requestId, userSession.User.Id, ct);
    }

    public Task SyncAsync(Guid requestId, SyncPayload payload, CancellationToken ct = default)
    {
        if (userSession.User is null) return Task.CompletedTask;
        return store.SyncAsync(requestId, userSession.User.Id, payload, ct);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
