using iPath.Domain.Entities;

namespace iPath.Application.Features.TaskAssignments;

public class TaskAssignmentCompletedEvent : ServiceRequestEvent, IEventWithNotifications;
