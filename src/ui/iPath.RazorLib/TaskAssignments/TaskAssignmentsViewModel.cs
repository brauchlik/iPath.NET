using iPath.Application.Features.TaskAssignments;
using iPath.Application.Querying;
using iPath.Domain.Entities;

namespace iPath.Blazor.Componenents.TaskAssignments;

public class TaskAssignmentsViewModel(IPathApi api, ISnackbar snackbar, IStringLocalizer T)
{
    public eTaskStatus? StatusFilter { get; set; }

    public async Task<TableData<TaskAssignmentDto>> GetPageAsync(TableState state, CancellationToken ct)
    {
        var query = state.BuildQuery(new GetUserTaskAssignmentsQuery
        {
            StatusFilter = StatusFilter,
            IncludeServiceRequest = true
        });
        var resp = await api.GetMyTaskAssignments(query);
        if (resp.IsSuccessful)
            return resp.Content.ToTableData();
        return new TableData<TaskAssignmentDto>();
    }

    // -- Queries --

    public async Task<IReadOnlyList<TaskAssignmentDto>?> GetCaseTaskAssignments(Guid serviceRequestId)
    {
        var resp = await api.GetCaseTaskAssignments(serviceRequestId);
        return resp.IsSuccessful ? resp.Content : null;
    }

    public async Task<IReadOnlyList<TaskAssignmentDto>?> GetGroupTaskAssignments(Guid groupId, eTaskStatus? statusFilter = null)
    {
        var resp = await api.GetGroupTaskAssignments(groupId, statusFilter);
        return resp.IsSuccessful ? resp.Content : null;
    }

    // -- Commands --

    public async Task AcceptTask(Guid id)
    {
        var resp = await api.AcceptTaskAssignment(id);
        if (resp.IsSuccessful)
            snackbar.Add(T["Task accepted"], Severity.Success);
        else
            snackbar.AddError(resp.ErrorMessage);
    }

    public async Task DeclineTask(Guid id)
    {
        var resp = await api.DeclineTaskAssignment(id);
        if (resp.IsSuccessful)
            snackbar.Add(T["Task declined"], Severity.Success);
        else
            snackbar.AddError(resp.ErrorMessage);
    }

    public async Task CompleteTask(Guid id)
    {
        var resp = await api.CompleteTaskAssignment(id);
        if (resp.IsSuccessful)
            snackbar.Add(T["Task completed"], Severity.Success);
        else
            snackbar.AddError(resp.ErrorMessage);
    }

    public async Task ReturnTask(Guid id)
    {
        var resp = await api.ReturnTaskAssignment(id);
        if (resp.IsSuccessful)
            snackbar.Add(T["Task returned for reassignment"], Severity.Success);
        else
            snackbar.AddError(resp.ErrorMessage);
    }

    public async Task<bool> ProposeTaskAssignment(Guid serviceRequestId, Guid assignedToUserId, eTaskAssignmentMode mode, string? notes = null)
    {
        var cmd = new ProposeTaskAssignmentCommand(
            ServiceRequestId: serviceRequestId,
            AssignedToUserId: assignedToUserId,
            Mode: mode,
            Notes: notes);
        var resp = await api.ProposeTaskAssignment(cmd);
        if (resp.IsSuccessful)
            return true;
        snackbar.AddWarning(resp.ErrorMessage);
        return false;
    }

    public async Task<bool> CancelTask(Guid id)
    {
        var resp = await api.CancelTaskAssignment(id);
        if (resp.IsSuccessful)
        {
            snackbar.Add(T["Task cancelled"], Severity.Success);
            return true;
        }
        snackbar.AddError(resp.ErrorMessage);
        return false;
    }

    public async Task<bool> CreateFollowUpTask(Guid serviceRequestId, string? notes = null)
    {
        var cmd = new CreateFollowUpTaskCommand(ServiceRequestId: serviceRequestId, Notes: notes);
        var resp = await api.CreateFollowUpTask(cmd);
        if (resp.IsSuccessful)
        {
            snackbar.AddInfo(T["A Follow-Up Task has been created."]);
            return true;
        }
        snackbar.AddWarning(resp.ErrorMessage);
        return false;
    }

    public static Color StatusColor(string status) => status switch
    {
        nameof(eTaskStatus.Proposed) => Color.Warning,
        nameof(eTaskStatus.Assigned) => Color.Info,
        nameof(eTaskStatus.InProgress) => Color.Primary,
        nameof(eTaskStatus.Completed) => Color.Success,
        nameof(eTaskStatus.Declined) => Color.Error,
        nameof(eTaskStatus.Cancelled) => Color.Error,
        nameof(eTaskStatus.ReturnedForReassignment) => Color.Warning,
        _ => Color.Default
    };
}
