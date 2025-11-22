namespace iPath.Domain.Notificxations;

public record NodeNofitication(Guid GroupId, 
    Guid NodeId, 
    Guid OwnerId, 
    DateTime EventDate, 
    eNodeEventType type,
    string message);


public enum eNodeEventType
{
    NewNode = 1,
    NewAttachment = 2,
    NodeDeleted = 3,
    NewAnnotation = 10,
    AnnotationDeleted = 11,
    Test = 99
}