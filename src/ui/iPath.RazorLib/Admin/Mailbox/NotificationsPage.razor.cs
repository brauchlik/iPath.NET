using iPath.Application;
using iPath.Application.Features.Notifications;
using iPath.Blazor.Componenents.Extensions;
using MudBlazor;

namespace iPath.Blazor.Componenents.Admin.Mailbox;

public partial class NotificationsPage(IPathApi api, ISnackbar snackbar, IDialogService dlg, IStringLocalizer T)
{
    public MudDataGrid<NotificationDto> grid;
    public eNotificationTarget Target = eNotificationTarget.None;

    public async Task<GridData<NotificationDto>> GetData(GridState<NotificationDto> state, CancellationToken ct = default)
    {
        try
        {
            if (!state.SortDefinitions.IsEmpty())
            {
                // TODO: make mapping generic
                var current = state.SortDefinitions.First();
                if (current.SortBy == "Date") 
                { 
                    var sd = new SortDefinition<NotificationDto>(
                        "CreatedOn",
                        current.Descending,
                        current.Index,
                        n => n.Date,
                        null
                    );
                    state.SortDefinitions.Clear();
                    state.SortDefinitions.Add(sd);
                }
                if (current.SortBy == "Receiver.Username") 
                { 
                    var sd = new SortDefinition<NotificationDto>(
                        "User.Username",
                        current.Descending,
                        current.Index,
                        n => n.Date,
                        null
                    );
                    state.SortDefinitions.Clear();
                    state.SortDefinitions.Add(sd);
                }
            }

            var query = state.BuildQuery(new GetNotificationsQuery { Target = Target });

            var pageSize = query.PageSize ?? 10;
            var resp = await api.GetNotifications(query.Page, pageSize, Target, query.Sorting);
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
        var res = await dlg.ShowMessageBoxAsync("Delete Notification",
        "Do you really want to delete all notifications?",
        yesText: "yes",
        cancelText: "cancel");
        if (res.HasValue && res.Value)
        {
            await api.DeleteAllNotifications();
            await grid.ReloadServerData();
        }
    }
}