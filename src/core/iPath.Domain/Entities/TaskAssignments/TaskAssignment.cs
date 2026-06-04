using iPath.Domain.Entities;

namespace iPath.Domain.Entities;

public class TaskAssignment : AuditableEntityWithEvents
{
    public Guid ServiceRequestId { get; set; }
    public ServiceRequest ServiceRequest { get; set; } = null!;

    public Guid AssignedToUserId { get; set; }
    public User AssignedToUser { get; set; } = null!;

    public Guid? AssignedByUserId { get; set; }
    public User? AssignedByUser { get; set; }

    public eTaskType Type { get; set; }
    public eTaskAssignmentMode Mode { get; set; }
    public eTaskStatus Status { get; set; }

    public string? Notes { get; set; }
    public DateTime? AcceptedOn { get; set; }
    public DateTime? CompletedOn { get; set; }
    public DateTime? Deadline { get; set; }
    public int? AttemptNumber { get; set; }

    public void Accept()
    {
        Status = eTaskStatus.Assigned;
        AcceptedOn = DateTime.UtcNow;
    }

    public void Decline()
    {
        Status = eTaskStatus.Declined;
    }

    public void StartWork()
    {
        if (Status is eTaskStatus.Assigned)
            Status = eTaskStatus.InProgress;
    }

    public void Complete()
    {
        Status = eTaskStatus.Completed;
        CompletedOn = DateTime.UtcNow;
    }

    public void ReturnForReassignment()
    {
        Status = eTaskStatus.ReturnedForReassignment;
    }

    public void Cancel()
    {
        Status = eTaskStatus.Cancelled;
    }
}
