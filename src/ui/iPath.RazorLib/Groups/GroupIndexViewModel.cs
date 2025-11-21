using iPath.Blazor.Componenents.Nodes;

namespace iPath.Blazor.Componenents.Groups;

public class GroupIndexViewModel(IPathApi api,
    ISnackbar snackbar, 
    IDialogService dialog,
    NodeViewModel nvm) : IViewModel
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
}
