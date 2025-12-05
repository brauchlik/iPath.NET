using iPath.Domain.Notificxations;

namespace iPath.API.Services.Notifications.Processors;

public interface INodeEventProcessor
{
    Task ProcessEvent(NodeEvent n, CancellationToken ct);
}
