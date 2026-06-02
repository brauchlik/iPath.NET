using iPath.Application.Features.Notifications;
using iPath.Domain.Notifications;

namespace iPath.Blazor.Componenents.Notifications;

public partial class NotificationBell : IDisposable
{
    List<NotificationDto> RecentUnread = new();

    protected override async Task OnInitializedAsync()
    {
        await LoadUnread();
        if (Sse is not null)
            Sse.NotificationReceived += OnNotificationReceived;
        AppState.OnChange += OnAppStateChanged;
    }

    private async void OnNotificationReceived(object? sender, NotificationDto dto)
    {
        await InvokeAsync(async () =>
        {
            await LoadUnread();
            StateHasChanged();
        });
    }

    private void OnAppStateChanged()
    {
        _ = InvokeAsync(StateHasChanged);
    }

    public void Dispose()
    {
        if (Sse is not null)
            Sse.NotificationReceived -= OnNotificationReceived;
        AppState.OnChange -= OnAppStateChanged;
    }

    async Task LoadUnread()
    {
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
