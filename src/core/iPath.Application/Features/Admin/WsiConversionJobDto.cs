using iPath.Domain.Entities;

namespace iPath.Application.Features.Admin;

public record GetWsiConversionJobsQuery()
    : IRequest<GetWsiConversionJobsQuery, Task<List<WsiConversionJobDto>>>;

public class WsiConversionJobDto
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public WsiConversionStatus Status { get; set; }
    public DateTime CreatedOn { get; set; }
    public DateTime? StartedOn { get; set; }
    public DateTime? CompletedOn { get; set; }
    public string? ErrorMessage { get; set; }
    public int RetryCount { get; set; }
    public string? Filename { get; set; }
}
