namespace iPath.Application.Features.CaseRoom;

public record ViewportState(double X, double Y, double Zoom);

public record Participant(Guid UserId, string DisplayName, DateTimeOffset JoinedAt);

public record SyncPayload(Guid? DocumentId, ViewportState? Viewport);

public record CaseRoomSnapshot(
    Guid RequestId,
    Guid? ActiveDocumentId,
    ViewportState? Viewport,
    Participant[] Participants);

public record CaseRoomStatus(bool IsActive, int ParticipantCount, string[] ParticipantNames);

public record CaseRoomSyncEvent(
    Guid RequestId,
    Guid UserId,
    string DisplayName,
    SyncPayload Payload,
    DateTimeOffset Timestamp);