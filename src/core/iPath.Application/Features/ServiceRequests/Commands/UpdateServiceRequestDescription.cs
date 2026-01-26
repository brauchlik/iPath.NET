using DispatchR.Abstractions.Send;

namespace iPath.Application.Features.ServiceRequests;

public record UpdateServiceRequestCommand(Guid NodeId, RequestDescription? Description, Guid? NewOwnerId = null, bool? IsDraft = null)
    : IRequest<UpdateServiceRequestCommand, Task<bool>>
    , IEventInput
{
    public string ObjectName => nameof(ServiceRequest);
}

public static partial class ServiceRequestCommandExtensions
{
    public static ServiceRequest UpdateNode(this ServiceRequest sr, UpdateServiceRequestCommand request, Guid userId)
    {
        bool isPublishEvent = false;
        if (sr.IsDraft && request.IsDraft.HasValue && !request.IsDraft.Value)
            isPublishEvent = true;

        if (request.Description is not null)
            sr.Description = request.Description;
        if (request.IsDraft.HasValue)
            sr.IsDraft = request.IsDraft.Value;

        sr.LastModifiedOn = DateTime.UtcNow;
        sr.CreateEvent<ServiceRequestDescriptionUpdatedEvent, UpdateServiceRequestCommand>(request, userId);
        if (isPublishEvent)
        {
            sr.CreateEvent<ServiceRequestNodePublishedEvent, UpdateServiceRequestCommand>(request, userId);
        }

        return sr;
    }
}