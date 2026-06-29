namespace iPath.Application.Features.CaseRoom;

public interface ICaseRoomSessionStore
{
    Task<CaseRoomSnapshot> JoinAsync(Guid requestId, Guid sessionId, Guid userId, string displayName, bool isGuest = false, CancellationToken ct = default);
    Task LeaveAsync(Guid requestId, Guid sessionId, CancellationToken ct);
    Task SyncAsync(Guid requestId, Guid sessionId, Guid userId, SyncPayload payload, CancellationToken ct);
    Task<CaseRoomStatus?> GetStatusAsync(Guid requestId, CancellationToken ct);
    Task<string> CreateShareTokenAsync(Guid requestId, CancellationToken ct);
    Task<bool> IsShareTokenValidAsync(Guid requestId, string token, CancellationToken ct);
}
