using System.Collections.Concurrent;
using iPath.Application.Features.CaseRoom;
using iPath.Application.Features.Notifications;
using iPath.API.Services.Notifications;
using Microsoft.Extensions.Logging;

namespace iPath.API.Services.CaseRoom;

public class CaseRoomSessionStore : ICaseRoomSessionStore, IDisposable
{
    private static readonly TimeSpan TeardownGrace = TimeSpan.FromSeconds(30);

    private readonly ISseConnectionManager _sseManager;
    private readonly INotificationEventBus _eventBus;
    private readonly ILogger<CaseRoomSessionStore> _logger;
    private readonly ConcurrentDictionary<Guid, SessionEntry> _sessions = new();
    private readonly System.Threading.PeriodicTimer _cleanupTimer = new(TimeSpan.FromSeconds(15));
    private readonly CancellationTokenSource _cleanupCts = new();

    public CaseRoomSessionStore(
        ISseConnectionManager sseManager,
        INotificationEventBus eventBus,
        ILogger<CaseRoomSessionStore> logger)
    {
        _sseManager = sseManager;
        _eventBus = eventBus;
        _logger = logger;
        _ = StartCleanupLoopAsync(_cleanupCts.Token);
    }

    public async Task<CaseRoomSnapshot> JoinAsync(Guid requestId, Guid userId, string displayName, CancellationToken ct)
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

        CaseRoomSnapshot snapshot;
        CaseRoomSyncEvent joinEvt;
        Guid[] participantIds;

        lock (entry)
        {
            // Cancel any pending teardown
            entry.TeardownCts?.Cancel();
            entry.TeardownCts = null;

            var participant = new Participant(userId, displayName, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
            entry.Session.Participants[userId] = participant;

            snapshot = BuildSnapshot(entry.Session);

            participantIds = entry.Session.Participants.Keys.ToArray();
            var updatedParticipants = entry.Session.Participants.Values.ToArray();
            var joinPayload = new SyncPayload(null, null, "Join", updatedParticipants);
            joinEvt = new CaseRoomSyncEvent(requestId, userId, displayName, joinPayload, DateTimeOffset.UtcNow);
        }

        // Broadcast to all participants; client-side SessionId filter handles dedup
        foreach (var pid in participantIds)
        {
            await _sseManager.SendToUserAsync(pid, "caseroom-sync", joinEvt);
        }
        _eventBus.PublishCaseRoomSync(joinEvt);

        return snapshot;
    }

    public async Task LeaveAsync(Guid requestId, Guid userId, CancellationToken ct)
    {
        if (!_sessions.TryGetValue(requestId, out var entry)) return;

        CaseRoomSyncEvent? leaveEvt = null;
        Guid[]? participantIds = null;
        bool scheduleTeardown = false;
        CancellationTokenSource? cts = null;

        lock (entry)
        {
            if (entry.Session.Participants.Remove(userId, out var removedParticipant))
            {
                var updatedParticipants = entry.Session.Participants.Values.ToArray();
                participantIds = entry.Session.Participants.Keys.ToArray();

                if (participantIds.Length > 0)
                {
                    var leavePayload = new SyncPayload(null, null, "Leave", updatedParticipants);
                    leaveEvt = new CaseRoomSyncEvent(requestId, userId, removedParticipant.DisplayName, leavePayload, DateTimeOffset.UtcNow);
                }
                else
                {
                    scheduleTeardown = true;
                    cts = new CancellationTokenSource(TeardownGrace);
                    entry.TeardownCts = cts;
                }
            }
        }

        if (leaveEvt is not null && participantIds is not null)
        {
            foreach (var pid in participantIds)
            {
                await _sseManager.SendToUserAsync(pid, "caseroom-sync", leaveEvt);
            }
            _eventBus.PublishCaseRoomSync(leaveEvt);
        }

        if (scheduleTeardown && cts is not null)
        {
            _ = Task.Delay(TeardownGrace, cts.Token).ContinueWith(t =>
            {
                lock (entry)
                {
                    if (entry.Session.Participants.Count == 0)
                    {
                        _sessions.TryRemove(requestId, out _);
                        _logger.LogInformation("Removed empty CaseRoom session {RequestId}", requestId);
                    }
                }
            }, CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
        }

        await Task.CompletedTask;
    }

    public async Task SyncAsync(Guid requestId, Guid userId, SyncPayload payload, CancellationToken ct)
    {
        if (!_sessions.TryGetValue(requestId, out var entry)) return;

        string displayName = "Unknown";
        lock (entry)
        {
            if (entry.Session.Participants.TryGetValue(userId, out var p))
            {
                var updatedParticipant = p with { LastSeenAt = DateTimeOffset.UtcNow };
                entry.Session.Participants[userId] = updatedParticipant;
                displayName = updatedParticipant.DisplayName;
            }

            if (payload.DocumentId.HasValue && entry.Session.ActiveDocumentId != payload.DocumentId)
            {
                entry.Session.ActiveDocumentId = payload.DocumentId;
            }
            if (payload.Viewport is not null)
            {
                entry.Session.CurrentViewport = payload.Viewport with { };
            }
        }

        var evt = new CaseRoomSyncEvent(requestId, userId, displayName, payload, DateTimeOffset.UtcNow);

        Guid[] participantIds;
        lock (entry)
        {
            participantIds = entry.Session.Participants.Keys.ToArray();
        }

        foreach (var participantId in participantIds)
        {
            await _sseManager.SendToUserAsync(participantId, "caseroom-sync", evt);
        }
        _eventBus.PublishCaseRoomSync(evt);

        _logger.LogDebug("CaseRoom {RequestId} sync from {UserId}: {Kind}",
            requestId, userId, payload.DocumentId.HasValue ? "document" : "viewport");
    }

    public Task<CaseRoomStatus?> GetStatusAsync(Guid requestId, CancellationToken ct)
    {
        if (!_sessions.TryGetValue(requestId, out var entry))
            return Task.FromResult<CaseRoomStatus?>(null);

        lock (entry)
        {
            var count = entry.Session.Participants.Count;
            return Task.FromResult<CaseRoomStatus?>(new CaseRoomStatus(
                IsActive: count > 0,
                ParticipantCount: count,
                ParticipantNames: entry.Session.Participants.Values.Select(p => p.DisplayName).ToArray()
            ));
        }
    }

    private async Task StartCleanupLoopAsync(CancellationToken ct)
    {
        try
        {
            while (await _cleanupTimer.WaitForNextTickAsync(ct))
            {
                var now = DateTimeOffset.UtcNow;
                var timeout = TimeSpan.FromSeconds(45);

                foreach (var kvp in _sessions)
                {
                    var requestId = kvp.Key;
                    var entry = kvp.Value;
                    List<Guid> toRemove = new();
                    CaseRoomSyncEvent? leaveEvt = null;
                    Guid[]? remainingIds = null;

                    lock (entry)
                    {
                        foreach (var p in entry.Session.Participants)
                        {
                            if (now - p.Value.LastSeenAt > timeout)
                            {
                                toRemove.Add(p.Key);
                            }
                        }

                        if (toRemove.Count > 0)
                        {
                            foreach (var uid in toRemove)
                            {
                                entry.Session.Participants.Remove(uid, out _);
                            }

                            var updatedParticipants = entry.Session.Participants.Values.ToArray();
                            remainingIds = entry.Session.Participants.Keys.ToArray();

                            var leavePayload = new SyncPayload(null, null, "Leave", updatedParticipants);
                            leaveEvt = new CaseRoomSyncEvent(requestId, Guid.Empty, "System", leavePayload, DateTimeOffset.UtcNow);
                        }

                        if (entry.Session.Participants.Count == 0 && entry.TeardownCts == null)
                        {
                            _sessions.TryRemove(requestId, out _);
                            _logger.LogInformation("Removed inactive empty CaseRoom session {RequestId}", requestId);
                        }
                    }

                    if (leaveEvt is not null && remainingIds is not null)
                    {
                        foreach (var pid in remainingIds)
                        {
                            await _sseManager.SendToUserAsync(pid, "caseroom-sync", leaveEvt);
                        }
                        _eventBus.PublishCaseRoomSync(leaveEvt);
                    }
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in CaseRoom cleanup loop");
        }
    }

    private static CaseRoomSnapshot BuildSnapshot(CaseRoomSessionData session) => new(
        session.RequestId,
        session.ActiveDocumentId,
        session.CurrentViewport,
        session.Participants.Values.ToArray()
    );

    public void Dispose()
    {
        _cleanupCts.Cancel();
        _cleanupTimer.Dispose();
        _cleanupCts.Dispose();
    }

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
