namespace iPath.Application.Features.ServiceRequests;



public record GetServiceRequestByIdQuery(Guid Id, bool inclDrafts = false)
    : IRequest<GetServiceRequestByIdQuery, Task<ServiceRequestDto>>;
