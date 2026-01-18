using iPath.Blazor.Componenents.ServiceRequests;

namespace iPath.Blazor.Componenents.ServiceRequests;

public class ServiceRequestListViewModel(IPathApi api,
    ISnackbar snackbar, 
    IDialogService dialog,
    NavigationManager nm,
    ServiceRequestViewModel nvm) : IViewModel
{
    public Guid? GroupId { get; set; }
    public Guid? OwnerId { get; set; }


    public string SearchString { get; set; }

    public async Task<TableData<ServiceRequestListDto>> GetNodeListAsync(TableState state, CancellationToken ct)
    {
        if (GroupId.HasValue || OwnerId.HasValue)
        { 
            var query = state.BuildQuery(new GetServiceRequestsQuery {
                GroupId = this.GroupId, 
                OwnerId = this.OwnerId,
                IncludeDetails = true,
                SearchString = this.SearchString
            });
            nvm.LastQuery = query;
            nvm.IdList = null;
            var resp = await api.GetRequestList(query);
            if (resp.IsSuccessful)
            {
                return resp.Content.ToTableData();
            }

            snackbar.AddError(resp.ErrorMessage);
        }
        return new TableData<ServiceRequestListDto>();
    }



    public async Task CreateNewCase()
    {
        if (GroupId.HasValue)
        {
            nm.NavigateTo($"request/create/{GroupId}");
        }
    }


    public void GotoNode(ServiceRequestListDto node)
        => nm.NavigateTo($"request/{node.Id}");


    public bool CreateNewCaseDisabled => !GroupId.HasValue;
}
