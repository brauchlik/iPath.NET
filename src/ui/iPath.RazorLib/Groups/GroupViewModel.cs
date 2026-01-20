using iPath.Application.Contracts;
using iPath.Blazor.Componenents.Shared;

namespace iPath.Blazor.Componenents.Groups;

public class GroupViewModel(IPathApi api,
    AppState appState,
    ISnackbar snackbar, 
    IDialogService dialog,
    ServiceRequestViewModel nvm) : IViewModel
{
    public GroupDto Model { get; private set; }

    public async Task LoadGroup(Guid id)
    {
        var resp = await api.GetGroup(id);
        if (resp.IsSuccessful)
        {
            Model = resp.Content;
        }
        else
        {
            snackbar.AddError(resp.ErrorMessage);
        }
    }


    public bool IsModerator => Model is not null && appState.IsGroupModerator(Model.Id);

    public async Task EditSettings()
    {
        if (Model is not null)
        {
            var p = new DialogParameters<GroupSettingsDialog> { { x => x.Model, Model.Settings } };
            DialogOptions opts = new() { MaxWidth = MaxWidth.Medium, FullWidth = false, NoHeader = false };
            var d = await dialog.ShowAsync<GroupSettingsDialog>("Group Settings", parameters: p, options: opts);
            var r = await d.Result;
            if (r?.Data is GroupSettings m)
            {
                var resp = await api.UpdateGroup(new UpdateGroupCommand { Id = Model.Id, Settings = m });
                snackbar.ShowIfError(resp);
                await LoadGroup(Model.Id);
            }
        }
    }
}
