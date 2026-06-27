using iPath.Application.Features.CaseRoom;
using iPath.Blazor.ServiceLib.Services;

namespace iPath.Blazor.Componenents.CaseRoom;

public class HttpCaseRoomSyncService(IPathApi api) : ICaseRoomSyncService
{
    public Task<CaseRoomSnapshot> JoinAsync(Guid requestId, CancellationToken ct = default)
        => api.JoinCaseRoom(requestId).ContinueWith(t => t.Result.Content!, ct);

    public Task LeaveAsync(Guid requestId, CancellationToken ct = default)
        => api.LeaveCaseRoom(requestId);

    public Task SyncAsync(Guid requestId, SyncPayload payload, CancellationToken ct = default)
        => api.SyncCaseRoom(requestId, payload);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
