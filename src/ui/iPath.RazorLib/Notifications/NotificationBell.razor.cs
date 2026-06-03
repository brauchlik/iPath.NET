using System.Text.Json;
using Microsoft.AspNetCore.Components;
using iPath.Application.Features.Notifications;
using iPath.Domain.Notifications;

namespace iPath.Blazor.Componenents.Notifications;

public partial class NotificationBell : IDisposable
{
    bool _open = false;
    List<NotificationDto> RecentUnread = new();

    void ToggleDrawer()
    {
        _open = !_open;
    }

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
        var resp = await Api.GetNotifications(0, 10, eNotificationTarget.InApp, ct: default);
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

    MarkupString GetNotificationHtml(NotificationDto n)
    {
        var data = DeserializePayload(n.Payload);
        if (data is null)
        {
            var fallback = n.EventType switch
            {
                eNodeNotificationType.NodePublished => T["A new case has been published"],
                eNodeNotificationType.NewAnnotation => T["A new annotation has been added"],
                _ => T["New notification"]
            };
            return new MarkupString(System.Web.HttpUtility.HtmlEncode(fallback));
        }

        var titleLine = string.Join(" — ",
            new[] { data.Title, data.BodySite }
            .Where(x => !string.IsNullOrEmpty(x)));

        var message = n.EventType switch
        {
            eNodeNotificationType.NodePublished => T["new case by"],
            eNodeNotificationType.NewAnnotation => T["new comment by"],
            _ => T["updated a case"]
        };

        var html = $"<strong>{System.Web.HttpUtility.HtmlEncode(titleLine)}</strong><br/>" +
                   $"{System.Web.HttpUtility.HtmlEncode(message)} {System.Web.HttpUtility.HtmlEncode(data.Sender)}<br />" +
                   $"<span class=\"mud-text-secondary\">{n.Date:g}</span>";
        return new MarkupString(html);
    }

    static NotificationPayload? DeserializePayload(string? payload)
    {
        if (payload is null) return null;
        try
        {
            return JsonSerializer.Deserialize(payload, NotificationPayloadSerializerContext.Default.NotificationPayload);
        }
        catch
        {
            return null;
        }
    }
}
