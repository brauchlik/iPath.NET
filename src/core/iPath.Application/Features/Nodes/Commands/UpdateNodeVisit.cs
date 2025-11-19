namespace iPath.Application.Features.Nodes;


public record UpdateNodeVisitCommand(Guid NodeId) : IRequest<UpdateNodeVisitCommand, Task<bool>>;

