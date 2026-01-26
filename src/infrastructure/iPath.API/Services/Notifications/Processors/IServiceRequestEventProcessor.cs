using iPath.Domain.Notificxations;

namespace iPath.API.Services.Notifications.Processors;

public interface IServiceRequestEventProcessor
{
    Task ProcessEvent(ServiceRequestEvent n, CancellationToken ct);
}
