using iPath.Application.Features.CaseRoom;
using iPath.Blazor.ServiceLib.Services;
using System.Net.Http.Json;

namespace iPath.Blazor.Componenents.CaseRoom;

public class HttpCaseRoomSyncService(IPathApi api) : ICaseRoomSyncService
{
    public async Task<CaseRoomSnapshot> JoinAsync(Guid requestId, Guid sessionId, string? token = null, Guid? initialDocumentId = null, bool? initialIsWSI = null, string? initialFilename = null, CancellationToken ct = default)
    {
        var response = await api.JoinCaseRoom(requestId, new SessionRequest(sessionId, initialDocumentId, initialIsWSI, initialFilename), token);
        if (!response.IsSuccessStatusCode || response.Content is null)
            throw new InvalidOperationException($"JoinCaseRoom failed: {response.StatusCode}");
        return response.Content;
    }

    public Task LeaveAsync(Guid requestId, Guid sessionId, CancellationToken ct = default)
        => api.LeaveCaseRoom(requestId, new SessionRequest(sessionId));

    public Task SyncAsync(Guid requestId, SyncPayload payload, CancellationToken ct = default)
        => api.SyncCaseRoom(requestId, payload);

    public Task<string> CreateShareTokenAsync(Guid requestId, Guid? initialDocumentId = null, bool? initialIsWSI = null, string? initialFilename = null, CancellationToken ct = default)
        => api.CreateShareToken(requestId, new SessionRequest(Guid.Empty, initialDocumentId, initialIsWSI, initialFilename)).ContinueWith(t => t.Result.Content!.Token, ct);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}