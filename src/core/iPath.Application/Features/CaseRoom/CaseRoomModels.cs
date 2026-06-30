namespace iPath.Application.Features.CaseRoom;

public record ViewportState(double X, double Y, double Zoom)
{
    public bool IsValid() => double.IsFinite(X) && double.IsFinite(Y) && double.IsFinite(Zoom) && Zoom > 0;
}

public record Participant(Guid SessionId, Guid UserId, string DisplayName, DateTimeOffset JoinedAt, DateTimeOffset LastSeenAt, bool IsGuest = false);

public record PointerState(double X, double Y, bool IsVisible);

public record SyncPayload(
    Guid? DocumentId,
    ViewportState? Viewport,
    Guid? SessionId = null,
    string? Action = null,
    Participant[]? Participants = null,
    Guid? ControllingSessionId = null,
    PointerState? Pointer = null);

public record CaseRoomSnapshot(
    Guid RequestId,
    Guid? ActiveDocumentId,
    ViewportState? Viewport,
    Participant[] Participants,
    Guid? ControllingSessionId = null,
    PointerState? Pointer = null);

public record CaseRoomStatus(bool IsActive, int ParticipantCount, string[] ParticipantNames);

public record SessionRequest(Guid SessionId);

public record CaseRoomSyncEvent(
    Guid RequestId,
    Guid UserId,
    string DisplayName,
    SyncPayload Payload,
    DateTimeOffset Timestamp);