namespace iPath.Application.Features.ServiceRequests;


public record CreateServiceRequestCommand(Guid GroupId, string RequestType, RequestDescription? Description = null, Guid? NodeId = null, Guid? OwnerId = null)
    : IRequest<CreateServiceRequestCommand, Task<ServiceRequestDto>>
    , IEventInput
{
    public string ObjectName => nameof(ServiceRequest);
}


public static partial class ServiceRequestCommandExtensions
{
    public static ServiceRequest CreateRequest(CreateServiceRequestCommand cmd, Guid userId)
    {
        var node = new ServiceRequest
        {
            Id = cmd.NodeId.HasValue ? cmd.NodeId.Value : Guid.CreateVersion7(),
            CreatedOn = DateTime.UtcNow,
            LastModifiedOn = DateTime.UtcNow,
            GroupId = cmd.GroupId,
            OwnerId = userId,
            Description = cmd.Description ?? new(),
            NodeType = cmd.RequestType,
            IsDraft = true
        };

        node.CreateEvent<ServiceRequestCreatedEvent, CreateServiceRequestCommand>(cmd, userId);
        return node;
    }
}

