namespace iPath.Application.Features.CaseRoom;

public interface ICaseRoomSyncService : IAsyncDisposable
{
    Task<CaseRoomSnapshot> JoinAsync(Guid requestId, Guid sessionId, string? token = null, Guid? initialDocumentId = null, bool? initialIsWSI = null, string? initialFilename = null, CancellationToken ct = default);
    Task LeaveAsync(Guid requestId, Guid sessionId, CancellationToken ct = default);
    Task SyncAsync(Guid requestId, SyncPayload payload, CancellationToken ct = default);
    Task<string> CreateShareTokenAsync(Guid requestId, Guid? initialDocumentId = null, bool? initialIsWSI = null, string? initialFilename = null, CancellationToken ct = default);
}

public interface ICaseRoomSyncReceiver
{
    IDisposable Subscribe(Guid requestId, Action<CaseRoomSyncEvent> handler);
}