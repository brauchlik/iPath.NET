namespace iPath.Application.Features.Nodes;



public record GetRootNodeByIdQuery(Guid Id, bool inclDrafts = false)
    : IRequest<GetRootNodeByIdQuery, Task<NodeDto>>;
