namespace iPath.Application.Features.CaseRoom;

public interface ICaseRoomSessionStore
{
    Task<CaseRoomSnapshot> JoinAsync(Guid requestId, Guid userId, string displayName, CancellationToken ct);
    Task LeaveAsync(Guid requestId, Guid userId, CancellationToken ct);
    Task SyncAsync(Guid requestId, Guid userId, SyncPayload payload, CancellationToken ct);
    Task<CaseRoomStatus?> GetStatusAsync(Guid requestId, CancellationToken ct);
}
