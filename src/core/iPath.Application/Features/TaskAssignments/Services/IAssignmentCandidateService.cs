namespace iPath.Application.Features.TaskAssignments;

public interface IAssignmentCandidateService
{
    Task<Guid?> FindBestCandidateAsync(Guid groupId, Guid serviceRequestId, CancellationToken ct = default);
    Task<List<Guid>> GetCandidateOrderAsync(Guid groupId, Guid serviceRequestId, CancellationToken ct = default);
}
