using iPath.Application.Features.CaseRoom;
using iPath.Blazor.ServiceLib.Services;

namespace iPath.Blazor.Componenents.CaseRoom;

public class HttpCaseRoomSyncService(IPathApi api) : ICaseRoomSyncService
{
    public Task<CaseRoomSnapshot> JoinAsync(Guid requestId, Guid sessionId, CancellationToken ct = default)
        => api.JoinCaseRoom(requestId, new SessionRequest(sessionId)).ContinueWith(t => t.Result.Content!, ct);

    public Task LeaveAsync(Guid requestId, Guid sessionId, CancellationToken ct = default)
        => api.LeaveCaseRoom(requestId, new SessionRequest(sessionId));

    public Task SyncAsync(Guid requestId, SyncPayload payload, CancellationToken ct = default)
        => api.SyncCaseRoom(requestId, payload);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}