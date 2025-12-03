using iPath.Application.Features.Notifications;

namespace iPath.Blazor.Componenents.Admin.Mailbox;

public partial class NotificationsPage(IPathApi api, ISnackbar snackbar, IDialogService dlg, IStringLocalizer T)
{
    public MudDataGrid<NotificationDto> grid;
    public eNotificationTarget Target = eNotificationTarget.Email;

    public async Task<GridData<NotificationDto>> GetData(GridState<NotificationDto> state)
    {
        try
        {
            var resp = await api.GetNotifications(state.Page, state.PageSize, Target);
            if (resp.IsSuccessful)
                return resp.Content.ToGridData();
        }
        catch (Exception ex)
        {
            snackbar.Add(ex.Message, Severity.Error);
        }
        return new GridData<NotificationDto>();
    }


    public async Task DeleteAll()
    {
        var res = await dlg.ShowMessageBox("Delete Mail",
        "Do you really want to delete all emails?",
        yesText: "yes",
        cancelText: "cancel");
        if (res.HasValue && res.Value)
        {
            await api.DeleteAllMail();
            await grid.ReloadServerData();
        }
    }
}