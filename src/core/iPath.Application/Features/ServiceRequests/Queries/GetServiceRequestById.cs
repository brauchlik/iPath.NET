namespace iPath.Application.Features.ServiceRequests;



public record GetServiceRequestByIdQuery(Guid Id, bool inclDrafts = false, bool inclDeletedData = false)
    : IRequest<GetServiceRequestByIdQuery, Task<ServiceRequestDto>>;
