using iPath.Blazor.Componenents.Admin.Users;

namespace iPath.Blazor.Componenents.Users;

public partial class UserNotificationGrid(UserAdminViewModel vm, IPathApi api, IStringLocalizer T, ISnackbar snackbar, IDialogService dialog)
{
    [Parameter]
    public UserDto? User { get; set; }

    [Parameter]
    public string CancelText { get; set; } = T["Back"];

    [Parameter]
    public EventCallback OnSaved { get; set; }

    List<UserNotificationModel> Items = new();


    protected override async Task OnParametersSetAsync()
    {
        await LoadData();
    }

    async Task LoadData()
    { 
        if (User is not null)
        {
            var resp = await api.GetUserNotification(User.Id);
            if( !resp.IsSuccessful )
            {
                snackbar.AddError(resp.ErrorMessage);
                return;
            }

            Items = resp.Content.Select(x => new UserNotificationModel(x)).ToList();
        }
    }

    async Task Save()
    {
        if (User is not null)
        {
            var dtos = Items.Select(n => n.ToDto()).ToArray();
            var cmd = new UpdateUserNotificationsCommand(User.Id, dtos);
            await vm.UpdateNotifications(cmd);
            await LoadData();
            await OnSaved.InvokeAsync();
        }
    }

    async Task ShowSettings(UserNotificationModel model)
    {
        if (model is not null)
        {
            var p = new DialogParameters<NotificationSettingsDialog> { { x => x.Model, model.Settings } };
            var o = new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true };
            var dlg = dialog.ShowAsync<NotificationSettingsDialog>("Settings", options: o, parameters: p);
            model.UpdateHasSettings(true);
            StateHasChanged();
        }
    }

    Color SaveButtonColor => Items.Any(m => m.HasChange) ? Color.Primary : Color.Default;
}


public class UserNotificationModel
{
    public readonly UserGroupNotificationDto Dto;

    public UserNotificationModel(UserGroupNotificationDto dto)
    {
        Dto = dto;

        NewCase =  dto.Source.HasFlag(eNotificationSource.NewCase);
        NewAnnotation = dto.Source.HasFlag(eNotificationSource.NewAnnotation);
        NewAnnotationOnMyCase = dto.Source.HasFlag(eNotificationSource.NewAnnotationOnMyCase);

        InApp = dto.Tartget.HasFlag(eNotificationTarget.InApp);
        Email = dto.Tartget.HasFlag(eNotificationTarget.Email);

        Settings = dto.Settings ?? new();
        UpdateHasSettings(false);
    }

    public Guid GroupId => Dto.GroupId;
    public string Groupname => Dto.Groupname;

    public NotificationSettings Settings { get; private set; }
    private NotificationSettings _origSettings;

    public bool NewCase { get; set; }
    public bool NewAnnotation { get; set; }
    public bool NewAnnotationOnMyCase { get; set; }

    public bool InApp {  get; set; }
    public bool Email { get; set; }


    public bool HasSettings { get; set; }
    private bool _hasSettingsUpdate;
    public void UpdateHasSettings(bool SetChanged)
    {
        if (Dto.Settings != null)
        {
            HasSettings = !String.IsNullOrEmpty(Dto.Settings?.IcdoTopoCode) || Dto.Settings.DailyEmailSummary;
        }
        _hasSettingsUpdate = SetChanged;
    }


    public bool HasChange
    {
        get
        {
            if (Dto.Source != Source) return true;
            if (Dto.Tartget != Target) return true;
            return _hasSettingsUpdate; 
        }
    }


    public string SettingsIcon => HasSettings ? Icons.Material.Filled.SettingsSuggest : Icons.Material.Filled.Settings;


    private eNotificationSource Source
    {
        get
        {
            eNotificationSource source = eNotificationSource.None;
            if (NewCase) source |= eNotificationSource.NewCase;
            if (NewAnnotation) source |= eNotificationSource.NewAnnotation;
            if (NewAnnotationOnMyCase) source |= eNotificationSource.NewAnnotationOnMyCase;
            return source;
        }
    }

    private eNotificationTarget Target
    {
        get
        {
            eNotificationTarget target = eNotificationTarget.None;
            if (InApp) target |= eNotificationTarget.InApp;
            if (Email) target |= eNotificationTarget.Email;
            return target;
        }
    }


    public UserGroupNotificationDto ToDto()
    {
        var res = Dto with { Source = Source, Tartget = Target, Settings = this.Settings };
        return res;
    }
}