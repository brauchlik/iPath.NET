using iPath.Application.Features.Notifications;
using iPath.Domain.Notifications;

namespace iPath.Blazor.Componenents.Notifications;

public partial class NotificationDropdown
{
    List<NotificationDto> RecentUnread = new();

    protected override async Task OnInitializedAsync()
    {
        await LoadUnread();
    }

    async Task LoadUnread()
    {
        // Get first page of InApp notifications for current user
        var resp = await Api.GetNotifications(0, 10, eNotificationTarget.InApp);
        if (resp.IsSuccessful && resp.Content is not null)
        {
            RecentUnread = resp.Content.Items.Where(n => n.ReadOn is null).ToList();
        }
    }

    async Task OpenNotification(NotificationDto n)
    {
        if (n.ReadOn is null)
        {
            await Api.MarkNotificationAsRead(n.Id);
            AppState.DecrementUnreadCount();
        }
        AppState.CloseNotificationDrawer();
        if (n.ServiceRequestId.HasValue)
        {
            Nav.NavigateTo($"request/{n.ServiceRequestId.Value}");
        }
    }

    async Task MarkAllAsRead()
    {
        await Api.MarkAllNotificationsAsRead();
        AppState.SetUnreadCount(0);
        RecentUnread.Clear();
    }

    string GetNotificationText(NotificationDto n) => n.EventType switch
    {
        eNodeNotificationType.NodePublished => T["A new case has been published"],
        eNodeNotificationType.NewAnnotation => T["A new annotation has been added"],
        _ => T["New notification"]
    };
}
