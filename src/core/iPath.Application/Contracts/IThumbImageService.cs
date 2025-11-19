namespace iPath.Application.Contracts;

public interface IThumbImageService
{
    ValueTask Handle(NodeThumnailNotCreatedNotification request, CancellationToken cancellationToken);
    Task<NodeFile> UpdateNodeAsync(NodeFile file, string filename);
}