using iPath.Blazor.Componenents.Users;

namespace iPath.Blazor.Componenents.Groups.Components;

public partial class GroupNotificationDialog : ComponentBase
{
    [CascadingParameter] private IMudDialogInstance MudDialog { get; set; } = null!;
    [Parameter] public Guid GroupId { get; set; }
    [Parameter] public string Groupname { get; set; } = string.Empty;
    [Parameter] public Guid UserId { get; set; }

    private bool InApp, Email;
    private bool NewCase, NewAnnotation, NewAnnotationOnMyCase;
    private NotificationSettings? Settings = new();
    private bool HasSettings;

    protected override async Task OnInitializedAsync()
    {
        var resp = await api.GetUserNotification(UserId);
        if (resp.IsSuccessful && resp.Content is not null)
        {
            var dto = resp.Content.FirstOrDefault(n => n.GroupId == GroupId);
            if (dto is not null)
            {
                InApp = dto.Tartget.HasFlag(eNotificationTarget.InApp);
                Email = dto.Tartget.HasFlag(eNotificationTarget.Email);
                NewCase = dto.Source.HasFlag(eNotificationSource.NewCase);
                NewAnnotation = dto.Source.HasFlag(eNotificationSource.NewAnnotation);
                NewAnnotationOnMyCase = dto.Source.HasFlag(eNotificationSource.NewAnnotationOnMyCase);
                Settings = dto.Settings ?? new();
                UpdateHasSettings();
            }
        }
    }

    private void UpdateHasSettings()
    {
        HasSettings = Settings?.BodySiteFilter is not null
                   || Settings?.DailyEmailSummary == true
                   || Settings?.UseProfileBodySiteFilter == true;
    }

    private string SettingsIcon => HasSettings
        ? Icons.Material.Filled.SettingsSuggest
        : Icons.Material.Filled.Settings;

    private string BodySiteFilterString
    {
        get
        {
            if (Settings?.BodySiteFilter is not null)
                return Settings.BodySiteFilter.ConceptCodesString;
            return "";
        }
    }

    private async Task ShowBodySiteFilter()
    {
        var dto = new UserGroupNotificationDto(UserId, GroupId, eNotificationSource.None, eNotificationTarget.None, Settings, Groupname);
        var model = new UserNotificationModel(dto, null!);
        var p = new DialogParameters<NotificationBodySiteFilterDialog> { { x => x.Model, model } };
        var o = new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true };
        var dlg = await dialog.ShowAsync<NotificationBodySiteFilterDialog>("Body Site Filter", options: o, parameters: p);
        var r = await dlg.Result;
        if (r is not null && !r.Canceled)
        {
            Settings = model.Settings;
            UpdateHasSettings();
            StateHasChanged();
        }
    }

    private async Task Save()
    {
        var source = eNotificationSource.None;
        if (NewCase) source |= eNotificationSource.NewCase;
        if (NewAnnotation) source |= eNotificationSource.NewAnnotation;
        if (NewAnnotationOnMyCase) source |= eNotificationSource.NewAnnotationOnMyCase;

        var target = eNotificationTarget.None;
        if (InApp) target |= eNotificationTarget.InApp;
        if (Email) target |= eNotificationTarget.Email;

        var dto = new UserGroupNotificationDto(UserId, GroupId, source, target, Settings, Groupname);
        var cmd = new UpdateUserNotificationsCommand(UserId, new[] { dto });
        var resp = await api.UpdateUserNotification(cmd);
        if (resp.IsSuccessful)
        {
            snackbar.Add(T["Notification settings saved"], Severity.Success);
            MudDialog.Close(DialogResult.Ok(true));
        }
        else
        {
            snackbar.AddError(resp.ErrorMessage);
        }
    }

    private void Cancel() => MudDialog.Cancel();
}
