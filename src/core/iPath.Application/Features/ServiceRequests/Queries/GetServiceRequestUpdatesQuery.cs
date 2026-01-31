namespace iPath.Application.Features.ServiceRequests;

public class GetServiceRequestUpdatesQuery : IRequest<GetServiceRequestUpdatesQuery, Task<ServiceRequestUpdatesDto>>
{
    public Guid? CommunityId { get; set; }
}
