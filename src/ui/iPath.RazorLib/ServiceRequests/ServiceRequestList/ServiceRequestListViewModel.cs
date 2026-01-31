using iPath.Blazor.Componenents.ServiceRequests;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace iPath.Blazor.Componenents.ServiceRequests;

public class ServiceRequestListViewModel(IPathApi api,
    ISnackbar snackbar, 
    IDialogService dialog,
    NavigationManager nm,
    ServiceRequestViewModel nvm) : IViewModel
{
    public Guid? GroupId { get; set; }
    public Guid? OwnerId { get; set; }
    public eRequestFilter ListMode { get; set; } = eRequestFilter.Group;


    public string SearchString { get; set; }

    public async Task<TableData<ServiceRequestListDto>> GetServiceRequestListAsync(TableState state, CancellationToken ct)
    {
        var query = state.BuildQuery(new GetServiceRequestsQuery
        {
            SearchString = this.SearchString,
            RequestFilter = ListMode,
            IncludeDetails = true
        });

        if (GroupId.HasValue)
        {
            query.GroupId = this.GroupId;
        }

        nvm.LastQuery = query;
        nvm.IdList = null;
        var resp = await api.GetRequestList(query);
        if (resp.IsSuccessful)
        {
            return resp.Content.ToTableData();
        }

        snackbar.AddError(resp.ErrorMessage);
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

