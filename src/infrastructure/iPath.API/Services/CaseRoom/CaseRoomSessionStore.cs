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

    public async Task<CaseRoomSnapshot> JoinAsync(Guid requestId, Guid sessionId, Guid userId, string displayName, bool isGuest = false, CancellationToken ct = default)
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
        Guid[] userIds;

        lock (entry)
        {
            entry.TeardownCts?.Cancel();
            entry.TeardownCts = null;

            var participant = new Participant(sessionId, userId, displayName, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, isGuest);
            entry.Session.Participants[sessionId] = participant;

            var sessions = entry.Session.UserSessions.GetOrAdd(userId, _ => new HashSet<Guid>());
            lock (sessions) { sessions.Add(sessionId); }

            snapshot = BuildSnapshot(entry.Session);

            userIds = entry.Session.UserSessions.Keys.ToArray();
            var updatedParticipants = entry.Session.Participants.Values.ToArray();
            var joinPayload = new SyncPayload(null, null, sessionId, "Join", updatedParticipants);
            joinEvt = new CaseRoomSyncEvent(requestId, userId, displayName, joinPayload, DateTimeOffset.UtcNow);
        }

        foreach (var uid in userIds)
        {
            await _sseManager.SendToUserAsync(uid, "caseroom-sync", joinEvt);
        }
        _eventBus.PublishCaseRoomSync(joinEvt);

        _logger.LogInformation("Session {SessionId} joined CaseRoom {RequestId} (user {UserId})",
            sessionId, requestId, userId);

        return snapshot;
    }

    public async Task LeaveAsync(Guid requestId, Guid sessionId, CancellationToken ct)
    {
        if (!_sessions.TryGetValue(requestId, out var entry)) return;

        CaseRoomSyncEvent? leaveEvt = null;
        Guid[]? userIds = null;
        bool scheduleTeardown = false;
        CancellationTokenSource? cts = null;

        lock (entry)
        {
            if (entry.Session.Participants.Remove(sessionId, out var removedParticipant))
            {
                var uid = removedParticipant.UserId;
                if (entry.Session.UserSessions.TryGetValue(uid, out var sessions))
                {
                    lock (sessions) { sessions.Remove(sessionId); }
                    if (sessions.Count == 0)
                        entry.Session.UserSessions.TryRemove(uid, out _);
                }

                // Check if any hosts remain
                var hasHosts = entry.Session.Participants.Values.Any(p => !p.IsGuest);
                if (!hasHosts)
                {
                    // Kick remaining guests
                    var guestSessionIds = entry.Session.Participants.Values.Where(p => p.IsGuest).Select(p => p.SessionId).ToList();
                    foreach (var gsid in guestSessionIds)
                    {
                        if (entry.Session.Participants.Remove(gsid, out var gRemoved))
                        {
                            if (entry.Session.UserSessions.TryGetValue(gRemoved.UserId, out var gSessions))
                            {
                                lock (gSessions) { gSessions.Remove(gsid); }
                                if (gSessions.Count == 0)
                                    entry.Session.UserSessions.TryRemove(gRemoved.UserId, out _);
                            }
                        }
                    }
                    entry.Session.ShareTokens.Clear();

                    var kickPayload = new SyncPayload(null, null, sessionId, "HostLeft", entry.Session.Participants.Values.ToArray());
                    leaveEvt = new CaseRoomSyncEvent(requestId, Guid.Empty, "System", kickPayload, DateTimeOffset.UtcNow);
                    scheduleTeardown = true;
                    cts = new CancellationTokenSource(TeardownGrace);
                    entry.TeardownCts = cts;
                }
                else
                {
                    var updatedParticipants = entry.Session.Participants.Values.ToArray();
                    userIds = entry.Session.UserSessions.Keys.ToArray();

                    if (userIds.Length > 0)
                    {
                        var leavePayload = new SyncPayload(null, null, sessionId, "Leave", updatedParticipants);
                        leaveEvt = new CaseRoomSyncEvent(requestId, uid, removedParticipant.DisplayName, leavePayload, DateTimeOffset.UtcNow);
                    }
                    else
                    {
                        scheduleTeardown = true;
                        cts = new CancellationTokenSource(TeardownGrace);
                        entry.TeardownCts = cts;
                    }
                }
            }
        }

        if (leaveEvt is not null)
        {
            if (userIds is not null)
            {
                foreach (var uid in userIds)
                {
                    if (uid != Guid.Empty)
                        await _sseManager.SendToUserAsync(uid, "caseroom-sync", leaveEvt);
                }
            }
            // Notify guest connections (registered under Guid.Empty); no-op if none
            await _sseManager.SendToUserAsync(Guid.Empty, "caseroom-sync", leaveEvt);
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
    }

    public async Task SyncAsync(Guid requestId, Guid sessionId, Guid userId, SyncPayload payload, CancellationToken ct)
    {
        if (!_sessions.TryGetValue(requestId, out var entry)) return;

        string displayName = "Unknown";
        lock (entry)
        {
            if (entry.Session.Participants.TryGetValue(sessionId, out var p))
            {
                var updatedParticipant = p with { LastSeenAt = DateTimeOffset.UtcNow };
                entry.Session.Participants[sessionId] = updatedParticipant;
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

        Guid[] userIds;
        lock (entry)
        {
            userIds = entry.Session.UserSessions.Keys.ToArray();
        }

        foreach (var uid in userIds)
        {
            await _sseManager.SendToUserAsync(uid, "caseroom-sync", evt);
        }
        _eventBus.PublishCaseRoomSync(evt);

        if (payload.Viewport is not null)
        {
            _logger.LogDebug("CaseRoom {RequestId} viewport update: Session {SessionId} (user {UserId}) -> X={X:F4}, Y={Y:F4}, Zoom={Zoom:F4}",
                requestId, sessionId, userId, payload.Viewport.X, payload.Viewport.Y, payload.Viewport.Zoom);
        }

        if (payload.DocumentId.HasValue)
        {
            _logger.LogInformation("CaseRoom {RequestId} document update: Session {SessionId} (user {UserId}) -> Document={DocumentId}",
                requestId, sessionId, userId, payload.DocumentId.Value);
        }
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

    public Task<string> CreateShareTokenAsync(Guid requestId, CancellationToken ct)
    {
        var entry = _sessions.GetOrAdd(requestId, rid => new SessionEntry
        {
            Session = new CaseRoomSessionData
            {
                RequestId = rid,
                CreatedAt = DateTimeOffset.UtcNow,
                CreatedBy = Guid.Empty
            }
        });

        var token = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16));
        entry.Session.ShareTokens[token] = 0;
        return Task.FromResult(token);
    }

    public Task<bool> IsShareTokenValidAsync(Guid requestId, string token, CancellationToken ct)
    {
        if (!_sessions.TryGetValue(requestId, out var entry))
        {
            _logger.LogWarning("IsShareTokenValidAsync failed: No active session found for requestId {RequestId}", requestId);
            return Task.FromResult(false);
        }

        if (!entry.Session.ShareTokens.ContainsKey(token))
        {
            _logger.LogWarning("IsShareTokenValidAsync failed: Session found, but token '{Token}' is not in ShareTokens list. Valid tokens count: {Count}", token, entry.Session.ShareTokens.Count);
            return Task.FromResult(false);
        }

        // Token is valid as long as there is at least one authenticated host
        var hasHost = entry.Session.Participants.Values.Any(p => !p.IsGuest);
        if (!hasHost)
        {
            _logger.LogWarning("IsShareTokenValidAsync failed: Token '{Token}' is valid, but no active host (presenter) was found in the room. Participants count: {Count}", token, entry.Session.Participants.Count);
        }
        return Task.FromResult(hasHost);
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
                            foreach (var sid in toRemove)
                            {
                                if (entry.Session.Participants.Remove(sid, out var removed))
                                {
                                    var uid = removed.UserId;
                                    if (entry.Session.UserSessions.TryGetValue(uid, out var sessions))
                                    {
                                        lock (sessions) { sessions.Remove(sid); }
                                        if (sessions.Count == 0)
                                            entry.Session.UserSessions.TryRemove(uid, out _);
                                    }
                                }
                            }

                            // Check if any hosts remain
                            var hasHosts = entry.Session.Participants.Values.Any(p => !p.IsGuest);
                            if (!hasHosts && entry.Session.Participants.Count > 0)
                            {
                                // Kick remaining guests
                                var guestSessionIds = entry.Session.Participants.Values.Where(p => p.IsGuest).Select(p => p.SessionId).ToList();
                                foreach (var gsid in guestSessionIds)
                                {
                                    if (entry.Session.Participants.Remove(gsid, out var gRemoved))
                                    {
                                        if (entry.Session.UserSessions.TryGetValue(gRemoved.UserId, out var gSessions))
                                        {
                                            lock (gSessions) { gSessions.Remove(gsid); }
                                            if (gSessions.Count == 0)
                                                entry.Session.UserSessions.TryRemove(gRemoved.UserId, out _);
                                        }
                                    }
                                }
                                entry.Session.ShareTokens.Clear();

                                var kickPayload = new SyncPayload(null, null, default, "HostLeft", entry.Session.Participants.Values.ToArray());
                                leaveEvt = new CaseRoomSyncEvent(requestId, Guid.Empty, "System", kickPayload, DateTimeOffset.UtcNow);
                            }
                            else
                            {
                                var updatedParticipants = entry.Session.Participants.Values.ToArray();
                                remainingIds = entry.Session.UserSessions.Keys.ToArray();

                                var leavePayload = new SyncPayload(null, null, default, "Leave", updatedParticipants);
                                leaveEvt = new CaseRoomSyncEvent(requestId, Guid.Empty, "System", leavePayload, DateTimeOffset.UtcNow);
                            }
                        }

                        if (entry.Session.Participants.Count == 0 && entry.TeardownCts == null)
                        {
                            _sessions.TryRemove(requestId, out _);
                            _logger.LogInformation("Removed inactive empty CaseRoom session {RequestId}", requestId);
                        }
                    }

                    if (leaveEvt is not null)
                    {
                        if (remainingIds is not null)
                        {
                            foreach (var uid in remainingIds)
                            {
                                if (uid != Guid.Empty)
                                    await _sseManager.SendToUserAsync(uid, "caseroom-sync", leaveEvt);
                            }
                        }
                        await _sseManager.SendToUserAsync(Guid.Empty, "caseroom-sync", leaveEvt);
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
        public ConcurrentDictionary<Guid, HashSet<Guid>> UserSessions { get; } = new();
        public ConcurrentDictionary<string, byte> ShareTokens { get; } = new(StringComparer.OrdinalIgnoreCase);
    }
}