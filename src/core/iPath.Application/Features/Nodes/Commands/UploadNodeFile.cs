using DispatchR.Abstractions.Send;
using iPath.Application.Contracts;
using Microsoft.Extensions.Logging;

namespace iPath.Application.Features.Nodes;


public record UploadNodeFileCommand(Guid ParentNodeId, string filename, long fileSize, Stream fileStream, string? contenttype = null)
    : IRequest<UploadNodeFileCommand, Task<NodeDto>>;



public record UploadNodeFileInput(Guid RootNodeId, Guid ParentNodeId, string filename) : IEventInput
{
    public string ObjectName => nameof(Node);
}
