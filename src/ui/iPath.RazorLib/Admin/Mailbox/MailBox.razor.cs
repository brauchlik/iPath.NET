namespace iPath.Blazor.Componenents.Admin.Mailbox;

public partial class MailBox(IPathApi api, ISnackbar snackbar, IDialogService dlg, IStringLocalizer T)
{
    public MudDataGrid<EmailMessage> grid;

    public async Task<GridData<EmailMessage>> GetData(GridState<EmailMessage> state, CancellationToken ct = default)
    {
        try
        {
            var resp = await api.GetMailBox(state.Page, state.PageSize);
            if (resp.IsSuccessful)
                return resp.Content.ToGridData();
        }
        catch (Exception ex)
        {
            snackbar.Add(ex.Message, Severity.Error);
        }
        return new GridData<EmailMessage>();
    }


    public async Task DeleteAll()
    {
        var res = await dlg.ShowMessageBoxAsync("Delete Mail",
        "Do you really want to delete all emails?",
        yesText: "yes",
        cancelText: "cancel");
        if (res.HasValue && res.Value)
        {
            await api.DeleteAllMail();
            await grid.ReloadServerData();
        }
    }

    public async Task Delete(EmailMessage msg)
    {
        await api.DeleteMail(msg.Id);
        await grid.ReloadServerData();
    }



    public async Task OnDetailsOpened(MudBlazor.Utilities.DataGridHierarchyVisibilityToggledEventArgs<EmailMessage> args)
    {
        if (args.Item != null && !args.Item.IsRead)
        {
            var resp = await api.SetMailAsRead(args.Item.Id);
            if (resp.IsSuccessful)
            {
                args.Item.IsRead = true;
            }
            else
            {
                snackbar.AddError(resp.ErrorMessage);
            }
        }
    }


    public async Task SendMail()
    {
        var o = new DialogOptions { MaxWidth = MaxWidth.Small };
        var d = await dlg.ShowAsync<SendMailDialog>("", o);
        var r = await d.Result;
        if (r is not null && r.Data is EmailMessage)
            snackbar.Add(String.Format(T["email to {0} has been queued"], (r.Data as EmailMessage).Receiver));
    }
}

public static class EmailMessageViewExtensions
{
    extension(EmailMessage msg)
    {
        public string ReadIcon => msg.IsRead ? Icons.Material.TwoTone.MarkEmailRead : Icons.Material.TwoTone.MarkAsUnread;
    }
}