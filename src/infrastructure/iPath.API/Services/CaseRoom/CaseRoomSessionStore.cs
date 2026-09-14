using System.Collections.Concurrent;
using iPath.Application.Features.CaseRoom;
using iPath.Application.Features.Notifications;
using iPath.API.Services.Notifications;
using Microsoft.Extensions.Logging;

namespace iPath.API.Services.CaseRoom;

public class CaseRoomSessionStore : ICaseRoomSessionStore, IDisposable
{
    private static readonly TimeSpan TeardownGrace = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Backstop lifetime for a guest share link. Host-left cleanup already clears tokens;
    /// this bounds a link that leaks after a room has been left open.
    /// </summary>
    private static readonly TimeSpan ShareTokenTtl = TimeSpan.FromHours(8);

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

    /// <summary>
    /// Fan-out addresses for a room's participants. Members are addressed by user id; guests by
    /// (room, session), because every guest carries the same synthetic Guid.Empty principal and
    /// addressing them by user id put all of them — across all rooms — in one bucket.
    /// Distinct(), so a member with several sessions is still written to once.
    /// </summary>
    private static string[] ChannelsFor(Guid requestId, IEnumerable<Participant> participants) =>
        participants
            .Select(p => p.IsGuest ? SseChannel.Guest(requestId, p.SessionId) : SseChannel.User(p.UserId))
            .Distinct()
            .ToArray();

    public async Task<CaseRoomSnapshot> JoinAsync(Guid requestId, Guid sessionId, Guid userId, string displayName, bool isGuest = false, Guid? initialDocumentId = null, bool? initialIsWSI = null, string? initialFilename = null, CancellationToken ct = default)
    {
        var entry = _sessions.GetOrAdd(requestId, rid => new SessionEntry
        {
            Session = new CaseRoomSessionData
            {
                RequestId = rid,
                CreatedAt = DateTimeOffset.UtcNow,
                CreatedBy = userId,
                ActiveDocumentId = initialDocumentId ?? Guid.Empty,
                ActiveDocumentIsWSI = initialIsWSI,
                ActiveDocumentFilename = initialFilename
            }
        });

        CaseRoomSnapshot snapshot;
        CaseRoomSyncEvent joinEvt;
        string[] channels;

        lock (entry)
        {
            entry.TeardownCts?.Cancel();
            entry.TeardownCts = null;

            var participant = new Participant(sessionId, userId, displayName, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, isGuest);
            entry.Session.Participants[sessionId] = participant;

            var sessions = entry.Session.UserSessions.GetOrAdd(userId, _ => new HashSet<Guid>());
            lock (sessions) { sessions.Add(sessionId); }

            if (!entry.Session.ControllingSessionId.HasValue)
            {
                entry.Session.ControllingSessionId = sessionId;
            }

            snapshot = BuildSnapshot(entry.Session);

            var updatedParticipants = entry.Session.Participants.Values.ToArray();
            channels = ChannelsFor(requestId, updatedParticipants);
            var joinPayload = new SyncPayload(null, null, sessionId, "Join", updatedParticipants, entry.Session.ControllingSessionId);
            joinEvt = new CaseRoomSyncEvent(requestId, userId, displayName, joinPayload, DateTimeOffset.UtcNow);
        }

        foreach (var channel in channels)
        {
            await _sseManager.SendToChannelAsync(channel, "caseroom-sync", joinEvt);
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
        string[]? channels = null;
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
                    // Capture the guests' channels before removing them — they are the audience
                    // for the HostLeft kick, and once removed there is nobody left to address.
                    channels = ChannelsFor(requestId, entry.Session.Participants.Values.Where(p => p.IsGuest));

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
                    entry.Session.ControllingSessionId = null;

                    var kickPayload = new SyncPayload(null, null, null, "HostLeft", entry.Session.Participants.Values.ToArray(), null);
                    leaveEvt = new CaseRoomSyncEvent(requestId, Guid.Empty, "System", kickPayload, DateTimeOffset.UtcNow);
                    scheduleTeardown = true;
                    cts = new CancellationTokenSource(TeardownGrace);
                    entry.TeardownCts = cts;
                }
                else
                {
                    if (entry.Session.ControllingSessionId == sessionId)
                        entry.Session.ControllingSessionId = null;

                    var updatedParticipants = entry.Session.Participants.Values.ToArray();
                    channels = ChannelsFor(requestId, updatedParticipants);

                    if (channels.Length > 0)
                    {
                        var leavePayload = new SyncPayload(null, null, sessionId, "Leave", updatedParticipants, entry.Session.ControllingSessionId);
                        leaveEvt = new CaseRoomSyncEvent(requestId, uid, removedParticipant.DisplayName, leavePayload, DateTimeOffset.UtcNow);
                    }
                    else
                    {
                        entry.Session.ControllingSessionId = null;
                        scheduleTeardown = true;
                        cts = new CancellationTokenSource(TeardownGrace);
                        entry.TeardownCts = cts;
                    }
                }
            }
        }

        if (leaveEvt is not null)
        {
            if (channels is not null)
            {
                foreach (var channel in channels)
                {
                    await _sseManager.SendToChannelAsync(channel, "caseroom-sync", leaveEvt);
                }
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
    }

    public async Task SyncAsync(Guid requestId, Guid sessionId, Guid userId, SyncPayload payload, CancellationToken ct)
    {
        if (!_sessions.TryGetValue(requestId, out var entry)) return;

        string displayName = "Unknown";
        SyncPayload broadcastPayload;

        lock (entry)
        {
            if (entry.Session.Participants.TryGetValue(sessionId, out var p))
            {
                var updatedParticipant = p with { LastSeenAt = DateTimeOffset.UtcNow };
                entry.Session.Participants[sessionId] = updatedParticipant;
                displayName = updatedParticipant.DisplayName;
            }

            if (payload.Action == "TakeControl")
            {
                entry.Session.ControllingSessionId = sessionId;
                broadcastPayload = new SyncPayload(null, null, sessionId, "ControllerChanged", null, sessionId, entry.Session.CurrentPointer);
            }
            else if (payload.Action == "ReleaseControl" && entry.Session.ControllingSessionId == sessionId)
            {
                entry.Session.ControllingSessionId = null;
                entry.Session.CurrentPointer = null;
                broadcastPayload = new SyncPayload(null, null, sessionId, "ControllerChanged", null, null, new PointerState(0, 0, false));
            }
            else
            {
                if (payload.Viewport is not null)
                {
                    if (entry.Session.ControllingSessionId.HasValue && entry.Session.ControllingSessionId != sessionId)
                        return; // non-controller viewport change — ignore

                    entry.Session.CurrentViewport = payload.Viewport with { };
                }

                if (payload.DocumentId.HasValue && entry.Session.ActiveDocumentId != payload.DocumentId)
                {
                    entry.Session.ActiveDocumentId = payload.DocumentId.Value;
                    entry.Session.ActiveDocumentIsWSI = payload.IsWSI;
                    entry.Session.ActiveDocumentFilename = payload.Filename;
                }

                if (payload.Pointer is not null)
                {
                    if (entry.Session.ControllingSessionId.HasValue && entry.Session.ControllingSessionId != sessionId)
                        return; // non-controller pointer change — ignore

                    entry.Session.CurrentPointer = payload.Pointer;
                }

                broadcastPayload = payload;
            }
        }

        var evt = new CaseRoomSyncEvent(requestId, userId, displayName, broadcastPayload, DateTimeOffset.UtcNow);

        string[] syncChannels;
        lock (entry)
        {
            syncChannels = ChannelsFor(requestId, entry.Session.Participants.Values);
        }

        foreach (var channel in syncChannels)
        {
            await _sseManager.SendToChannelAsync(channel, "caseroom-sync", evt);
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

        if (payload.Action == "TakeControl" || payload.Action == "ReleaseControl")
        {
            _logger.LogInformation("CaseRoom {RequestId} control change: Session {SessionId} (user {UserId}) -> {Action}",
                requestId, sessionId, userId, payload.Action);
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

    public Task<string> CreateShareTokenAsync(Guid requestId, Guid? initialDocumentId = null, bool? initialIsWSI = null, string? initialFilename = null, CancellationToken ct = default)
    {
        var entry = _sessions.GetOrAdd(requestId, rid => new SessionEntry
        {
            Session = new CaseRoomSessionData
            {
                RequestId = rid,
                CreatedAt = DateTimeOffset.UtcNow,
                CreatedBy = Guid.Empty,
                ActiveDocumentId = initialDocumentId ?? Guid.Empty,
                ActiveDocumentIsWSI = initialIsWSI,
                ActiveDocumentFilename = initialFilename
            }
        });

        var token = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16));
        entry.Session.ShareTokens[token] = DateTimeOffset.UtcNow;
        return Task.FromResult(token);
    }

    public Task<bool> IsShareTokenValidAsync(Guid requestId, string token, CancellationToken ct)
    {
        if (!_sessions.TryGetValue(requestId, out var entry))
        {
            _logger.LogWarning("IsShareTokenValidAsync failed: No active session found for requestId {RequestId}", requestId);
            return Task.FromResult(false);
        }

        if (!entry.Session.ShareTokens.TryGetValue(token, out var issuedAt))
        {
            _logger.LogWarning("IsShareTokenValidAsync failed: Session found, but token '{Token}' is not in ShareTokens list. Valid tokens count: {Count}", token, entry.Session.ShareTokens.Count);
            return Task.FromResult(false);
        }

        if (DateTimeOffset.UtcNow - issuedAt > ShareTokenTtl)
        {
            entry.Session.ShareTokens.TryRemove(token, out _);
            _logger.LogWarning("IsShareTokenValidAsync failed: token for {RequestId} expired (issued {IssuedAt})", requestId, issuedAt);
            return Task.FromResult(false);
        }

        // A guest link only grants access while a real participant is hosting the room.
        // Without this, a token issued against an empty session lets a guest in alone.
        if (!entry.Session.Participants.Values.Any(p => !p.IsGuest))
        {
            _logger.LogWarning("IsShareTokenValidAsync failed: no host present in room {RequestId}", requestId);
            return Task.FromResult(false);
        }

        return Task.FromResult(true);
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
                    string[]? remainingChannels = null;

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
                                // Capture the guests' channels before removing them — they are
                                // the audience for the HostLeft kick.
                                remainingChannels = ChannelsFor(requestId, entry.Session.Participants.Values.Where(p => p.IsGuest));

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
                                entry.Session.ControllingSessionId = null;

                                var kickPayload = new SyncPayload(null, null, null, "HostLeft", entry.Session.Participants.Values.ToArray(), null);
                                leaveEvt = new CaseRoomSyncEvent(requestId, Guid.Empty, "System", kickPayload, DateTimeOffset.UtcNow);
                            }
                            else
                            {
                                // Clear controller if any timed-out session was the controller
                                if (toRemove.Any(sid => entry.Session.ControllingSessionId == sid))
                                    entry.Session.ControllingSessionId = null;

                                var updatedParticipants = entry.Session.Participants.Values.ToArray();
                                remainingChannels = ChannelsFor(requestId, updatedParticipants);

                                var leavePayload = new SyncPayload(null, null, null, "Leave", updatedParticipants, entry.Session.ControllingSessionId);
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
                        if (remainingChannels is not null)
                        {
                            foreach (var channel in remainingChannels)
                            {
                                await _sseManager.SendToChannelAsync(channel, "caseroom-sync", leaveEvt);
                            }
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
        session.Participants.Values.ToArray(),
        session.ControllingSessionId,
        session.CurrentPointer,
        session.ActiveDocumentIsWSI,
        session.ActiveDocumentFilename
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
        public Guid ActiveDocumentId { get; set; }
        public bool? ActiveDocumentIsWSI { get; set; }
        public string? ActiveDocumentFilename { get; set; }
        public ViewportState? CurrentViewport { get; set; }
        public Guid? ControllingSessionId { get; set; }
        public PointerState? CurrentPointer { get; set; }
        public DateTimeOffset CreatedAt { get; init; }
        public Guid CreatedBy { get; init; }
        public ConcurrentDictionary<Guid, Participant> Participants { get; } = new();
        public ConcurrentDictionary<Guid, HashSet<Guid>> UserSessions { get; } = new();
        /// <summary>Share token -> time it was issued, so tokens can expire.</summary>
        public ConcurrentDictionary<string, DateTimeOffset> ShareTokens { get; } = new(StringComparer.OrdinalIgnoreCase);
    }
}