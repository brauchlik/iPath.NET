using Humanizer;
using iPath.Application.Contracts;
using iPath.Application.Features.Nodes;
using iPath.Blazor.Componenents.Nodes.Annotations;
using iPath.Blazor.Componenents.Shared;
using iPath.Blazor.ServiceLib.ApiClient;
using iPath.Domain.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Refit;

namespace iPath.Blazor.Componenents.Nodes;

public class NodeViewModel(IPathApi api, 
    AppState appState,
    ISnackbar snackbar, 
    IDialogService srvDialog, 
    IStringLocalizer T,
    NavigationManager nm,
    QuestionnaireCache qCache,
    ILogger<NodeViewModel> logger)
    : IViewModel
{
    // Signal State Changes to Views
    public event Action OnChange;
    private void NotifyStateChanged() => OnChange?.Invoke();

    public event Action OnLoadingStarted;
    public event Action OnLoadingFinished;


    public string SearchString { get; set; }

    public NodeDto RootNode { get; private set; }
    public NodeDto? SelectedNode { get; private set; }
    public GroupDto? ActiveGroup {  get; private set; }

    public bool IsRootNodeSelected
    {
        get
        {
            if (RootNode is null) return false;
            if (SelectedNode is null) return true;
            return SelectedNode.Id == RootNode.Id;
        }
    }

    public async Task SelectChilNode(NodeDto? child)
    {
        if (child is null)
        {
            SelectedNode = null;
        }
        else if (RootNode is null)
        {
            // if the child is itself a root node => select it, otherwise throw
            if (child.RootNodeId is null)
            {
                RootNode = child;
                SelectedNode = child;
            }
            else
            {
                throw new Exception("cannot select child node without loading a root node first");
            }
        }
        else if (child.RootNodeId.HasValue && child.RootNodeId != RootNode.Id)
        {
            throw new Exception("child node is not child of the root node");
        }
        else 
        { 
            SelectedNode = child;
        }
        NotifyStateChanged();
    }


    public void ClearData()
    {
        RootNode = null;
        SelectedNode = null;
        NotifyStateChanged();
    }

    public async Task LoadNode(Guid id)
    {
        OnLoadingStarted?.Invoke();
        var respN = await api.GetNodeById(id);
        if (respN.IsSuccessful)
        {
            RootNode = respN.Content;
            // after loading a new root node (Case) => select the root node
            await SelectChilNode(RootNode);

            // load Group
            if (RootNode.GroupId.HasValue && (ActiveGroup is null || ActiveGroup.Id != RootNode.GroupId))
            {
                var respG = await api.GetGroup(RootNode.GroupId.Value);
                if (respG.IsSuccessful)
                    ActiveGroup = respG.Content;
            }
        }
        else
        {
            snackbar.AddWarning(respN.ErrorMessage);
        }
        OnLoadingFinished?.Invoke();
    }

    public async Task ReloadNode()
    {
        if (RootNode != null)
        {
            var id = RootNode.Id;
            RootNode = null;
            await LoadNode(id);
        }
    }



    public async Task MarkAsVisited()
    {
        if (RootNode is not null)
        {
            await api.UpdateNodeVisit(RootNode.Id);
        }
    }


    #region "-- Navigation --"
    public GetNodesQuery LastQuery { get; set; }
    public string NavUrl
    {
        get
        {
            if (LastQuery != null)
            {
                if (LastQuery.GroupId.HasValue)
                    return $"groups/{LastQuery.GroupId}";
                else if (LastQuery.OwnerId.HasValue)
                    return "mycases";
            }
            return "groups";
        }
    }
    public List<Guid>? IdList { get; set; } = null;

    public async ValueTask<bool> LoadIdList()
    {
        if (IdList is null && LastQuery is not null)
        {
            var cmd = new GetNodeIdListQuery(LastQuery);
            var resp = await api.GetNodeIdList(cmd);
            if (resp.IsSuccessful)
            {
                IdList = resp.Content.ToList();
            }
            else
            {
                IdList = new List<Guid>();
            }
            return true;
        }
        return false;
    }

    public async Task GoUp()
    {
        if (RootNode is null) return;

        if (IsRootNodeSelected)
        {
            // go back to nodelist (group, search, mynode)
            nm.NavigateTo(NavUrl);
        }
        else if (SelectedNode != null && SelectedNode.ParentNodeId.HasValue)
        {
            // go up to parent
            var tmp = RootNode.ChildNodes.FirstOrDefault(n => n.Id == SelectedNode.ParentNodeId.Value);
            SelectedNode = tmp is null ? RootNode : tmp;
        }
    }

    public async Task GoNext()
    {
        if (RootNode is null) return;

        if (IsRootNodeSelected)
        {
            if (await LoadIdList())
            {
                var idx = IdList.IndexOf(RootNode.Id);
                if (idx < IdList.Count() - 1)
                {
                    // there is one more in list => select
                    var nextId = IdList[idx + 1];
                    ClearData();
                    nm.NavigateTo($"node/{nextId}");
                    return;
                }
            }
            // otherwhise go up
            await GoUp();
        }
        else
        {
            // find index of current child Node in parents child list
            var list = RootNode.ChildNodes.Where(n => n.ParentNodeId == SelectedNode.ParentNodeId).OrderBy(n => n.SortNr).ToList();
            var idx = list.IndexOf(SelectedNode);
            if (idx < list.Count() - 1)
            {
                // there is one more in list => select
                nm.NavigateTo($"node/{IdList[idx - 1]}");
            }
            else
            {
                // otherwhise go up
                await GoUp();
            }
        }
    }

    public async Task GoPrevious()
    {
        if (RootNode is null) return;

        if (IsRootNodeSelected)
        {
            // next in list => to be done
            if (await LoadIdList())
            {
                var idx = IdList.IndexOf(RootNode.Id);
                if (idx > 0)
                {
                    // there is one more in list => select
                    await LoadNode(IdList[idx - 1]);
                    return;
                }
            }
            // otherwhise go up
            await GoUp();
        }
        else
        {
            // find index of current child Node in parents child list
            var list = RootNode.ChildNodes.Where(n => n.ParentNodeId == SelectedNode.ParentNodeId).OrderBy(n => n.SortNr).ToList();
            var idx = list.IndexOf(SelectedNode);
            if (idx > 0)
            {
                // there is one more in list => select
                SelectedNode = list[idx - 1];
            }
            else
            {
                // otherwhise go up
                await GoUp();
            }
        }
    }
    #endregion


    #region  "-- Actions --"


    public bool SortingDisabled = false;

    public async Task SaveChildNodeSortOrder(Dictionary<Guid, int> sortOrder)
    {
        if (RootNode != null)
        {
            var cmd = new UpdateChildNodeSortOrderCommand(RootNode.Id, sortOrder);
            var resp = await api.UpdateNodeSortOrder(cmd);
            if (!resp.IsSuccessful)
            {
                snackbar.AddWarning(resp.ErrorMessage);
            }
        }
    }


    public bool CreateNewDisabled => false;

    public async Task CreateNewNode(Guid GroupId)
    {
        ClearData();
        var resp = await api.CreateNode(new CreateNodeCommand(GroupId: GroupId, NodeType: "Case"));
        if (resp.IsSuccessful)
        {
            RootNode = resp.Content;
            IsEditing = true;
        }
        else
        {
            snackbar.AddError(resp.ErrorMessage);
        }
    }

    public async Task CreateNew()
    {
        snackbar.AddWarning("not implemented");
    }



    public bool DeleteDisabled => EditDisabled;

    public async Task Delete(NodeDto? node = null, bool AskConfirmation = true)
    {
        // Delete root node if none îs selected
        node ??= RootNode;

        if (AskConfirmation) {
            bool? result = await srvDialog.ShowMessageBox(
                T["Warning"],
                T["Are you sure that you want to delete !"],
                yesText: T["Yes"], cancelText: T["Cancel"]);
            if (result is null)
                return;
        }
        if (node != null)
        {
            var resp = await api.DeleteNode(node.Id);
            if (resp.IsSuccessful)
            {
                // if we delete the root node in edit mode => go back to group
                if (IsRootNodeSelected && IsEditing)
                {
                    IsEditing = false;
                    await GoUp();
                }
                else
                {
                    if (RootNode.ChildNodes.Contains(node))
                    {
                        RootNode.ChildNodes.Remove(node);
                        OnChange();
                    }
                }
            }
            else
            {
                snackbar.AddError(resp.ErrorMessage);
            }
        }
    }

    public async Task Delete(HashSet<Guid> ids, bool AskConfirmation = true)
    {
        if (RootNode is null) return;

        if (AskConfirmation)
        {
            bool? result = await srvDialog.ShowMessageBox(
                T["Warning"],
                string.Format(T["Are you sure that you want to delete {0} items !"], ids.Count()),
                yesText: T["Yes"], cancelText: T["Cancel"]);
            if (result is null)
                return;
        }


        var errors = new List<string>();
        foreach( var id in ids)
        {
            var node = RootNode.ChildNodes.FirstOrDefault(n => n.Id == id);
            if (node != null)
            {
                var resp = await api.DeleteNode(node.Id);
                if (resp.IsSuccessful)
                {
                    RootNode.ChildNodes.Remove(node);
                }
                else
                {
                    errors.Add(resp.ErrorMessage); 
                }
            }
        }

        if (errors.Any())
        {
            snackbar.AddWarning(errors.FirstOrDefault());
        }
        else
        {
            snackbar.Add(T["Items deleted"], Severity.Success);
        }
        OnChange();
    }


    public bool IsEditing { 
        get;
        set { field = value; OnChange(); } 
    }

    public bool EditDisabled => !appState.CanEditNode(SelectedNode);

    public async Task Edit(NodeDto? node = null)
    {
        if (!EditDisabled && RootNode is not null)
        {
            nm.NavigateTo($"node/edit/{RootNode.Id}");
        }
    }


    public bool SaveDisabled => !IsEditing;

    public async Task Save()
    {
        if (!SaveDisabled && IsEditing)
        {
            var cmd = new UpdateNodeCommand(RootNode.Id, RootNode.Description, false);
            var resp = await api.UpdateNode(cmd);
            if (!resp.IsSuccessful)
            {
                snackbar.AddError(resp.ErrorMessage);
            }
            else
            {
                nm.NavigateTo($"node/{RootNode.Id}");
            }
            IsEditing = false;
            OnChange();
        }
    }

    public async Task CancelEdit()
    {
        if (RootNode != null)
        {
            IsEditing = false;

            if (RootNode.IsDraft)
            {
                // when cancelling a draft, save current state ...
                var cmd = new UpdateNodeCommand(RootNode.Id, RootNode.Description, false);
                var resp = await api.UpdateNode(cmd);
                // and go up
                await GoUp();
            }
            else
            {
                nm.NavigateTo($"node/{RootNode.Id}");
            }
        }
    }





    public bool AttachFileDisabled => EditDisabled;


    public async Task UploadFile(IBrowserFile f)
    {
        try
        {
            if (SelectedNode is null)
            {
                snackbar.AddWarning("no node selected");
            }
            else if (f.Size > IPathApi.MaxFileSize)
            {
                snackbar.Add("File is larger then " + IPathApi.MaxFileSize.Bytes().Megabytes);
            }
            else
            {
                logger.LogInformation("starting file upload: " + f.Name);

                var stream = new StreamPart(f.OpenReadStream(maxAllowedSize: IPathApi.MaxFileSize), f.Name, f.ContentType);
                var resp = await api.UploadNodeFile(stream, SelectedNode.Id);
                if (resp.IsSuccessful)
                {
                    // append to child nodes
                    RootNode.ChildNodes.Add(resp.Content);
                    OnChange();
                }
                else
                {
                    snackbar.AddWarning(resp.ErrorMessage);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError("Upload Error", ex);
            snackbar.AddError(ex.Message);
        }
    }



    public bool AnnotateDisabled => IsEditing;

    public async Task Annotate()
    {
        var parameters = new DialogParameters<NodeAddAnnotationDialog> { };
        var dialog = await srvDialog.ShowAsync<NodeAddAnnotationDialog>("New Annotation", parameters);
        var result = await dialog.Result;
        if (!result.Canceled && result.Data != null)
        {
            var text = result.Data.ToString().Trim();

            if (string.IsNullOrEmpty(text)) return;

            var cmd = new CreateNodeAnnotationCommand(RootNode.Id, text, null);

            var resp = await api.CreateAnnotation(cmd);
            if (resp.IsSuccessful)
            {
                await ReloadNode();
            }
        }
    }


    public bool DeleteAnnotationDisabled => true;

    public async Task DeleteAnnotation(AnnotationDto item)
    {
        snackbar.AddWarning("not implemented");
    }


    #endregion


    public async Task<string> GetQuesiotnnaire(Guid Id)
    {
        return await qCache.GetQuestionnaireResourceAsync(Id);
    }
}

