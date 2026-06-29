namespace iPath.Application.Features.ServiceRequests;

public record GetAdjacentServiceRequestIdQuery(
    Guid CurrentId,
    int Direction,
    GetServiceRequestsQueryBase Query
) : IRequest<GetAdjacentServiceRequestIdQuery, Task<Guid?>>;