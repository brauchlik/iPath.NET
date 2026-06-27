using System.Collections.Concurrent;
using iPath.Application.Features.CaseRoom;
using iPath.Application.Features.Notifications;
using iPath.API.Services.Notifications;
using Microsoft.Extensions.Logging;

namespace iPath.API.Services.CaseRoom;

public class CaseRoomSessionStore(
    ISseConnectionManager sseManager,
    INotificationEventBus eventBus,
    ILogger<CaseRoomSessionStore> logger) : ICaseRoomSessionStore
{
    private static readonly TimeSpan TeardownGrace = TimeSpan.FromSeconds(30);

    private readonly ConcurrentDictionary<Guid, SessionEntry> _sessions = new();

    public Task<CaseRoomSnapshot> JoinAsync(Guid requestId, Guid userId, string displayName, CancellationToken ct)
    {
        var entry = _sessions.GetOrAdd(requestId, rid => new SessionEntry
        {
            Session = new CaseRoomSessionData
            {
                RequestId = rid,
                CreatedAt = DateTimeOffset.UtcNow,
                CreatedBy = userId
            }
        });

        entry.TeardownCts?.Cancel();
        entry.TeardownCts = null;

        entry.Session.Participants.TryAdd(userId, new Participant(userId, displayName, DateTimeOffset.UtcNow));

        return Task.FromResult(BuildSnapshot(entry.Session));
    }

    public async Task LeaveAsync(Guid requestId, Guid userId, CancellationToken ct)
    {
        if (!_sessions.TryGetValue(requestId, out var entry)) return;
        entry.Session.Participants.Remove(userId, out _);

        if (entry.Session.Participants.Count == 0)
        {
            var cts = new CancellationTokenSource(TeardownGrace);
            entry.TeardownCts = cts;
            _ = Task.Delay(TeardownGrace, cts.Token).ContinueWith(t =>
            {
                if (entry.Session.Participants.Count == 0)
                    _sessions.TryRemove(requestId, out _);
            }, CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
        }

        await Task.CompletedTask;
    }

    public async Task SyncAsync(Guid requestId, Guid userId, SyncPayload payload, CancellationToken ct)
    {
        if (!_sessions.TryGetValue(requestId, out var entry)) return;

        if (payload.DocumentId.HasValue && entry.Session.ActiveDocumentId != payload.DocumentId)
        {
            entry.Session.ActiveDocumentId = payload.DocumentId;
        }
        if (payload.Viewport is not null)
        {
            entry.Session.CurrentViewport = payload.Viewport with { };
        }

        var displayName = entry.Session.Participants.TryGetValue(userId, out var p)
            ? p.DisplayName : "Unknown";
        var evt = new CaseRoomSyncEvent(requestId, userId, displayName, payload, DateTimeOffset.UtcNow);

        foreach (var participantId in entry.Session.Participants.Keys)
        {
            if (participantId == userId) continue;
            await sseManager.SendToUserAsync(participantId, "caseroom-sync", evt);
        }
        eventBus.PublishCaseRoomSync(evt);

        logger.LogDebug("CaseRoom {RequestId} sync from {UserId}: {Kind}",
            requestId, userId, payload.DocumentId.HasValue ? "document" : "viewport");
    }

    public Task<CaseRoomStatus?> GetStatusAsync(Guid requestId, CancellationToken ct)
    {
        if (!_sessions.TryGetValue(requestId, out var entry))
            return Task.FromResult<CaseRoomStatus?>(null);

        var count = entry.Session.Participants.Count;
        return Task.FromResult<CaseRoomStatus?>(new CaseRoomStatus(
            IsActive: count > 0,
            ParticipantCount: count,
            ParticipantNames: entry.Session.Participants.Values.Select(p => p.DisplayName).ToArray()
        ));
    }

    private static CaseRoomSnapshot BuildSnapshot(CaseRoomSessionData session) => new(
        session.RequestId,
        session.ActiveDocumentId,
        session.CurrentViewport,
        session.Participants.Values.ToArray()
    );

    private sealed class SessionEntry
    {
        public required CaseRoomSessionData Session { get; init; }
        public CancellationTokenSource? TeardownCts { get; set; }
    }

    private sealed class CaseRoomSessionData
    {
        public Guid RequestId { get; init; }
        public Guid? ActiveDocumentId { get; set; }
        public ViewportState? CurrentViewport { get; set; }
        public DateTimeOffset CreatedAt { get; init; }
        public Guid CreatedBy { get; init; }
        public ConcurrentDictionary<Guid, Participant> Participants { get; } = new();
    }
}
