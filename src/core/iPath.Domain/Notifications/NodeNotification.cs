namespace iPath.Domain.Notificxations;

public record NodeNofitication(
    Guid NodeId, 
    Guid? UserId,
    Guid? OwnerId,
    Guid? GroupId,
    DateTime EventDate, 
    eNodeEventType type,
    string message);


public enum eNodeEventType
{
    NewNode = 0,
    NodePublished = 1,
    NewAttachment = 2,
    NodeDeleted = 3,
    NewAnnotation = 10,
    AnnotationDeleted = 11,
    Test = 99
}