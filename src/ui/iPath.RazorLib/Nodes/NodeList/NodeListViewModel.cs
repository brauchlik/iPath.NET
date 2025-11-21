using iPath.Application.Features.Nodes;
using iPath.Blazor.Componenents.Nodes;

namespace iPath.Blazor.Componenents.Nodes;

public class NodeListViewModel(IPathApi api,
    ISnackbar snackbar, 
    IDialogService dialog,
    NodeViewModel nvm) : IViewModel
{
    public Guid? GroupId { get; set; }
    public Guid? OwnerId { get; set; }


    public string SearchString { get; set; }

    public async Task<TableData<NodeListDto>> GetNodeListAsync(TableState state, CancellationToken ct)
    {
        if (GroupId.HasValue || OwnerId.HasValue)
        { 
            var query = state.BuildQuery(new GetNodesQuery { GroupId = GroupId, OwnerId = OwnerId });
            nvm.LastQuery = query;
            nvm.IdList = null;
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
