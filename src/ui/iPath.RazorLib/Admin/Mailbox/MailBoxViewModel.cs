using iPath.Blazor.ServiceLib.ApiClient;
using iPath.Domain.Entities;
using Microsoft.Extensions.Localization;

namespace iPath.Blazor.Componenents.Admin;

public class MailBoxViewModel(IPathApi api, ISnackbar snackbar, IDialogService dlg, IStringLocalizer T)
    : IViewModel
{
    public MudDataGrid<EmailMessage> grid;

    public async Task<GridData<EmailMessage>> GetData(GridState<EmailMessage> state)
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
}

public static class EmailMessageViewExtensions
{
    extension (EmailMessage msg)
    {
        public string ReadIcon => msg.IsRead ? Icons.Material.TwoTone.MarkEmailRead : Icons.Material.TwoTone.MarkAsUnread;
    }
}