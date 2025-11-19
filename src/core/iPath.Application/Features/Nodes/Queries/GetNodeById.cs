namespace iPath.Application.Features.Nodes;



public record GetRootNodeByIdQuery(Guid Id)
    : IRequest<GetRootNodeByIdQuery, Task<NodeDto>>;
