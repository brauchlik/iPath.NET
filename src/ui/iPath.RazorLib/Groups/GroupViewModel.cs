using iPath.Application.Contracts;
using iPath.Blazor.Componenents.Shared;

namespace iPath.Blazor.Componenents.Groups;

public class GroupViewModel(IPathApi api,
    AppState appState,
    IGroupCache cache,
    ISnackbar snackbar,
    IDialogService dialog,
    IStringLocalizer T,
    ILogger<GroupViewModel> logger,
    ServiceRequestListViewModel srvm) : IViewModel
{
    public GroupDto Model { get; private set; }

    public async Task LoadGroup(Guid id)
    {
        // only reload on group change
        if (Model == null || Model.Id != id)
        {
            var resp = await api.GetGroup(id);
            if (resp.IsSuccessful)
            {
                // reset search string when opening a new group
                srvm.SearchString = string.Empty;
                Model = resp.Content;
                logger.LogInformation("Group {group} loaded", Model.Name);
            }
            else
            {
                snackbar.AddError(resp.ErrorMessage);
            }
        }
    }


    public bool IsModerator => Model is not null && appState.IsGroupModerator(Model.Id);

    public async Task EditSettings()
    {
        if (Model is not null)
        {
            var p = new DialogParameters<GroupSettingsDialog> { { x => x.Model, Model.Settings } };
            DialogOptions opts = new() { MaxWidth = MaxWidth.Medium, FullWidth = true, NoHeader = false };
            var d = await dialog.ShowAsync<GroupSettingsDialog>("Group Settings", parameters: p, options: opts);
            var r = await d.Result;
            if (r?.Data is GroupSettings m)
            {
                var resp = await api.UpdateGroup(new UpdateGroupCommand { Id = Model.Id, Settings = m });
                snackbar.ShowIfError(resp);
                await LoadGroup(Model.Id);
            }
            await cache.ClearGroup(Model.Id);
        }
    }



    public bool IsAdmin => appState.IsAdmin;

    public async Task DeleteDrafts()
    {
        if (Model is not null)
        {
            bool? result = await dialog.ShowMessageBoxAsync(
                            T["Warning"],
                            T["Are you sure that you want to delete all drafts in this group !"],
                            yesText: T["Yes"], cancelText: T["Cancel"]);
            if (result is null)
                return;

            var resp = await api.DeleteGroupDrafts(Model.Id);
            snackbar.ShowIfError(resp);
        }
    }
}