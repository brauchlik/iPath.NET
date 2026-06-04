using iPath.Domain.Entities;

namespace iPath.Application.Features.TaskAssignments;

public class TaskAssignmentDto
{
    public Guid Id { get; init; }
    public Guid ServiceRequestId { get; init; }
    public string? Title { get; init; }
    public Guid GroupId { get; init; }
    public string? GroupName { get; init; }

    public Guid AssignedToUserId { get; init; }
    public string? AssignedToUsername { get; init; }

    public Guid? AssignedByUserId { get; init; }
    public string? AssignedByUsername { get; init; }

    public string Type { get; init; } = default!;
    public string Mode { get; init; } = default!;
    public string Status { get; init; } = default!;

    public string? Notes { get; init; }
    public DateTime CreatedOn { get; init; }
    public DateTime? AcceptedOn { get; init; }
    public DateTime? CompletedOn { get; init; }
    public DateTime? Deadline { get; init; }
}

public static class TaskAssignmentDtoExtensions
{
    public static TaskAssignmentDto ToDto(this TaskAssignment ta)
    {
        return new TaskAssignmentDto
        {
            Id = ta.Id,
            ServiceRequestId = ta.ServiceRequestId,
            Title = ta.ServiceRequest?.Description?.Title,
            GroupId = ta.ServiceRequest?.GroupId ?? Guid.Empty,
            GroupName = ta.ServiceRequest?.Group?.Name,
            AssignedToUserId = ta.AssignedToUserId,
            AssignedToUsername = ta.AssignedToUser?.UserName,
            AssignedByUserId = ta.AssignedByUserId,
            AssignedByUsername = ta.AssignedByUser?.UserName,
            Type = ta.Type.ToString(),
            Mode = ta.Mode.ToString(),
            Status = ta.Status.ToString(),
            Notes = ta.Notes,
            CreatedOn = ta.CreatedOn,
            AcceptedOn = ta.AcceptedOn,
            CompletedOn = ta.CompletedOn,
            Deadline = ta.Deadline
        };
    }
}
