using iPath.Application.Features.Notifications;
using iPath.Domain.Notifications;
using MudBlazor;

namespace iPath.Blazor.Componenents.Notifications;

public partial class NotificationPage : IDisposable
{
    MudDataGrid<NotificationDto> grid = null!;

    protected override async Task OnInitializedAsync()
    {
        if (Sse is not null)
            Sse.NotificationReceived += OnNotificationReceived;
    }

    private async void OnNotificationReceived(object? sender, NotificationDto dto)
    {
        await InvokeAsync(async () =>
        {
            if (grid is not null)
                await grid.ReloadServerData();
        });
    }

    public void Dispose()
    {
        if (Sse is not null)
            Sse.NotificationReceived -= OnNotificationReceived;
    }

    public async Task<GridData<NotificationDto>> GetData(GridState<NotificationDto> state, CancellationToken ct = default)
    {
        var resp = await Api.GetNotifications(state.Page, state.PageSize, eNotificationTarget.InApp);
        if (resp.IsSuccessful && resp.Content is not null)
            return resp.Content.ToGridData();
        return new GridData<NotificationDto>();
    }

    async Task OnRowClick(DataGridRowClickEventArgs<NotificationDto> args)
    {
        await MarkAsRead(args.Item);
        if (args.Item.ServiceRequestId.HasValue)
        {
            Nav.NavigateTo($"request/{args.Item.ServiceRequestId.Value}");
        }
    }

    async Task OnDetailsOpened(MudBlazor.Utilities.DataGridHierarchyVisibilityToggledEventArgs<NotificationDto> args)
    {
        await MarkAsRead(args.Item);
    }

    async Task MarkAsRead(NotificationDto dto)
    {
        if (dto.ReadOn is null)
        {
            await Api.MarkNotificationAsRead(dto.Id);
            AppState.DecrementUnreadCount();
        }
    }

    async Task MarkAllAsRead()
    {
        var res = await Dialog.ShowMessageBoxAsync(T["Mark all as read"],
            T["Do you want to mark all notifications as read?"],
            yesText: T["Yes"], cancelText: T["Cancel"]);
        if (res.HasValue && res.Value)
        {
            await Api.MarkAllNotificationsAsRead();
            AppState.SetUnreadCount(0);
            await grid.ReloadServerData();
        }
    }

    public async Task Delete(NotificationDto n)
    {
        await Api.DeleteNotification(n.Id);
        if (n.ReadOn is null)
            AppState.DecrementUnreadCount();
        await grid.ReloadServerData();
    }

    async Task DeleteAll()
    {
        var res = await Dialog.ShowMessageBoxAsync(T["Delete all"],
            T["Do you really want to delete all notifications?"],
            yesText: T["Yes"], cancelText: T["Cancel"]);
        if (res.HasValue && res.Value)
        {
            await Api.DeleteAllNotifications();
            AppState.SetUnreadCount(0);
            await grid.ReloadServerData();
        }
    }
}
