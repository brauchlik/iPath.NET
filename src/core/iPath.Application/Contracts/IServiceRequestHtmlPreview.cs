using iPath.Application.Features.Notifications;

namespace iPath.Application.Contracts;

public interface IServiceRequestHtmlPreview
{
    string Name { get; }
    string CreatePreview(NotificationDto n, ServiceRequestDto dto);
}
