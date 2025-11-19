using iPath.EF.Core.FeatureHandlers.Nodes.Commands;

namespace iPath.EF.Core.FeatureHandlers.Nodes.Queries;

public record GetNodeFileQuery(Guid nodeId) 
    : IRequest<GetNodeFileQuery, Task<FetchFileResponse>>;
