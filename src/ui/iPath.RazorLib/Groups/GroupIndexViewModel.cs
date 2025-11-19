using iPath.Application.Features.Nodes;

namespace iPath.Blazor.Componenents.Groups;

public class GroupIndexViewModel(IPathApi api, ISnackbar snackbar, IDialogService dialog) : IViewModel
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


    public string SearchString { get; set; }

    public async Task<TableData<NodeListDto>> GetNodeListAsync(TableState state, CancellationToken ct)
    {
        if (Model is not null)
        {
            var query = state.BuildQuery(new GetNodesQuery { GroupId = Model.Id });
            var resp = await api.GetNodeList(query);
            if (resp.IsSuccessful)
            {
                return resp.Content.ToTableData();
            }

            snackbar.AddError(resp.ErrorMessage);
        }
        return new TableData<NodeListDto>();
    }
}
