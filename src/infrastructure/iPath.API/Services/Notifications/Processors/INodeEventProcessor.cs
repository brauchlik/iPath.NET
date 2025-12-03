using iPath.Domain.Notificxations;

namespace iPath.API.Services.Notifications.Processors;

public interface INodeEventProcessor
{
    Task ProcessEvent(NodeNofitication n, CancellationToken ct);
}
