using iPath.Application.Features.Notifications;

namespace iPath.Application.Contracts;

public interface IServiceRequestHtmlPreview
{
    string Name { get; }
    Task<string> CreatePreview(NotificationDto n, ServiceRequestDto dto);
    string CreateUrl(ServiceRequestDto dto);
}
