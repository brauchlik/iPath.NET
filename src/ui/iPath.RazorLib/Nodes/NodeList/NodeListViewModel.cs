using iPath.Application.Features.Nodes;
using iPath.Blazor.Componenents.Nodes;
using iPath.Blazor.ServiceLib.ApiClient;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace iPath.Blazor.Componenents.Nodes;

public class NodeListViewModel(IPathApi api,
    ISnackbar snackbar, 
    IDialogService dialog,
    NavigationManager nm,
    NodeViewModel nvm) : IViewModel
{
    public Guid? GroupId { get; set; }
    public Guid? OwnerId { get; set; }


    public string SearchString { get; set; }

    public async Task<TableData<NodeListDto>> GetNodeListAsync(TableState state, CancellationToken ct)
    {
        if (GroupId.HasValue || OwnerId.HasValue)
        { 
            var query = state.BuildQuery(new GetNodesQuery {
                GroupId = this.GroupId, 
                OwnerId = this.OwnerId,
                IncludeDetails = true,
                SearchString = this.SearchString
            });
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



    public async Task CreateNewCase()
    {
        if (GroupId.HasValue)
        {
            nm.NavigateTo($"node/new/{GroupId}");
        }
    }



    public bool CreateNewCaseDisabled => !GroupId.HasValue;
}
