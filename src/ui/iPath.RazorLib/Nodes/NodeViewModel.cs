using Humanizer;
using iPath.Application;
using iPath.Application.Contracts;
using iPath.Blazor.Componenents.Nodes.Annotations;
using iPath.Blazor.Componenents.Shared;
using Microsoft.AspNetCore.Components.Forms;
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
    public GroupDto? ActiveGroup { get; private set; }

    public bool IsRootNodeSelected
    {
        get
        {
            if (RootNode is null) return false;
            if (SelectedNode is null) return true;
            return SelectedNode.Id == RootNode.Id;
        }
    }


    public void SelectChilNode(Guid id)
    {
        SelectChilNode(RootNode?.ChildNodes?.FirstOrDefault(c => c.Id == id));
    }

    public void SelectChilNode(NodeDto? child)
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
        // check if this is just the root or a child node and if so select it
        if (RootNode is not null && RootNode.Id == id)
        {
            SelectChilNode(RootNode);
        }
        else if (RootNode is not null && RootNode.ContainsChildId(id))
        {
            SelectChilNode(id);
        }
        else
        {
            OnLoadingStarted?.Invoke();
            var respN = await api.GetNodeById(id);
            if (respN.IsSuccessful)
            {
                RootNode = respN.Content;
                // after loading a new root node (Case) => select the root node
                SelectChilNode(RootNode);

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
        }
        return !IdList.IsEmpty();
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
            NavigateToChilNode(tmp is null ? RootNode : tmp);
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
        else if (SelectedNode is not null)
        {
            // find index of current child Node in parents child list
            var list = RootNode.ChildNodes.Where(n => n.ParentNodeId == SelectedNode.ParentNodeId).OrderBy(n => n.SortNr).ToList();
            var idx = list.IndexOf(SelectedNode);
            if (idx < list.Count() - 1)
            {
                // there is one more in list => select
                NavigateToChilNode(list[idx + 1]);
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
                    var prevId = IdList[idx - 1];
                    ClearData();
                    nm.NavigateTo($"node/{prevId}");
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
                NavigateToChilNode(list[idx - 1]);
            }
            else
            {
                // otherwhise go up
                await GoUp();
            }
        }
    }

    public void NavigateToChilNode(NodeDto childNode)
    {
        nm.NavigateTo($"node/{childNode.Id}");
    }




    public async Task SelectNextImage()
    {
        if (RootNode is null) return;

        if (SelectedNode is not null)
        {
            // find index of current child Node
            var list = RootNode.ChildNodes.Where(n => n.IsImage).OrderBy(n => n.SortNr).ToList();
            if (list.IsEmpty()) await GoUp();
            var idx = list.IndexOf(SelectedNode);
            if (idx < list.Count() - 1)
            {
                // there is one more in list => select
                SelectedNode = list[idx + 1];
            }
            else
            {
                // goto first
                SelectedNode = list.First();
            }
        }
    }

    public async Task SelectPreviousImage()
    {
        if (RootNode is null) return;

        if (SelectedNode is not null)
        {
            // find index of current child Node
            var list = RootNode.ChildNodes.Where(n => n.IsImage).OrderBy(n => n.SortNr).ToList();
            if (list.IsEmpty()) await GoUp();
            var idx = list.IndexOf(SelectedNode);
            if (idx > 0)
            {
                // there is one more in list => select
                SelectedNode = list[idx - 1];
            }
            else
            {
                // goto last
                SelectedNode = list.Last();
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

    public async Task CreateNewNode(Guid GroupId, string? QuestionaireId = null)
    {
        ClearData();

        // set active group (templates, options, etc)
        var grpResp = await api.GetGroup(GroupId);
        if (!grpResp.IsSuccessful)
        {
            snackbar.AddError(grpResp.ErrorMessage);
            return;
        }
        ActiveGroup = grpResp.Content;

        // Create new Node
        var resp = await api.CreateNode(new CreateNodeCommand(GroupId: GroupId, NodeType: "Case"));
        if (resp.IsSuccessful)
        {
            RootNode = resp.Content;
            if (!string.IsNullOrEmpty(QuestionaireId))
            {
                RootNode.Description.Questionnaire = new QuestionnaireResponseData { QuestionnaireId = QuestionaireId };
            }
            SelectChilNode(RootNode);
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

        if (AskConfirmation)
        {
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
                // if we delete the root node => end edit and go back to group
                if (IsRootNodeSelected)
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
        foreach (var id in ids)
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


    public bool IsEditing
    {
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

    public delegate Task BeforeSaveEventHandler(object? sender);
    public event BeforeSaveEventHandler? OnBeforeSaveEvent;

    public async Task Save()
    {
        if (OnBeforeSaveEvent is not null)
            await OnBeforeSaveEvent.Invoke(this);  

        if (RootNode != null && !SaveDisabled && IsEditing)
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
                IApiResponse resp;
                // if title is empty => delete draft
                if (string.IsNullOrEmpty(RootNode.Description.Title))
                {
                    resp = await api.DeleteNode(RootNode.Id);
                }
                else
                {
                    // when cancelling a draft, save current state ...
                    var cmd = new UpdateNodeCommand(RootNode.Id, RootNode.Description, false);
                    resp = await api.UpdateNode(cmd);
                }

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


    public bool AnnotationsHide => ActiveGroup is not null && ActiveGroup.Settings.AnnotationsHide;

    public bool AnnotateDisabled => IsEditing;

    public async Task Annotate(Guid? ChildNodeId = null)
    {
        if (AnnotateDisabled) return;

        var model = CreateNewAnnotationInput(ChildNodeId);
        var parameters = new DialogParameters<AddPlaintextAnnotationDialog> { { x => x.Model, model } };
        var dialog = await srvDialog.ShowAsync<AddPlaintextAnnotationDialog>("New Comment", parameters);
        var result = await dialog.Result;
        if (!result.Canceled && result.Data is AnnotationEditModel data)
        {
            if (data.Data.ValidateInput())
            {
                await SubmitAnnotation();
            }
        }
    }

    public AnnotationEditModel? NewAnnotation { get; set; }
    public async Task StartAnnotation()
    {
        NewAnnotation = CreateNewAnnotationInput();
        OnChange?.Invoke();
    }

    private AnnotationEditModel CreateNewAnnotationInput(Guid? ChildNodeId = null)
    {
        var model = new AnnotationEditModel();
        if (RootNode is not null)
        {
            model.RootNodeId = RootNode.Id;
            model.ChildNodeId = ChildNodeId ;
            if (ActiveGroup is not null)
            {
                model.AskMorphology = ActiveGroup.Settings.AnnotationHasMoprhoogy;
            }
        }
        return model;
    }


    public async Task CancelAnnotation()
    {
        NewAnnotation = null;
        OnChange?.Invoke();
    }

    public async Task SubmitAnnotation()
    {
        if (NewAnnotation is not null)
        {
            var cmd = new CreateNodeAnnotationCommand(NewAnnotation.RootNodeId, NewAnnotation.Data, NewAnnotation.ChildNodeId);

            var resp = await api.CreateAnnotation(cmd);
            if (resp.IsSuccessful)
            {
                NewAnnotation = null;
                await ReloadNode();
            }
            else
            {
                snackbar.AddError(resp.ErrorMessage);
            }
        }
        OnChange?.Invoke();
    }


    public bool DeleteAnnotationDisabled => true;

    public async Task DeleteAnnotation(AnnotationDto item)
    {
        snackbar.AddWarning("not implemented");
    }



    public bool HasImages => RootNode is not null && RootNode.ChildNodes.Any(n => n.IsImage);
    public void StartSlideshow()
    {
        if (RootNode is not null)
        {
            nm.NavigateTo($"node/{RootNode.Id}/slideshow");
        }
    }

    #endregion


    public async Task<string?> GetQuesiotnnaire(NodeDescription model)
    {
        if (model.Questionnaire != null)
        {
            return await qCache.GetQuestionnaireResourceAsync(model.Questionnaire.QuestionnaireId, model.Questionnaire.Version);
        }
        return null;
    }

    public async Task<string?> GetQuesiotnnaire(string questionnaireId, int? version = null)
    {
        return await qCache.GetQuestionnaireResourceAsync(questionnaireId, version);
    }



    public IEnumerable<QuestionnaireForGroupDto> AvailableAnnotationQuestionnaires(eAnnotationType annotationTyp)
    {
        if (ActiveGroup is null) return new List<QuestionnaireForGroupDto>();

        var qUsage = annotationTyp switch
        {
            eAnnotationType.Comment => eQuestionnaireUsage.Annotation,
            eAnnotationType.FinalAssesment => eQuestionnaireUsage.FinalAssesment,
            eAnnotationType.FollowUp => eQuestionnaireUsage.FollowUp,
            _ => eQuestionnaireUsage.None,
        };

        var ret = ActiveGroup.Questionnaires
            .Where(q => q.Usage == qUsage)
            .OrderBy(q => q.QuestinnaireName)
            .ToList();
        if (ret.Any())
        {
            ret.Insert(0, PlainText);
        }
        return ret;
    }

    private QuestionnaireForGroupDto PlainText => new QuestionnaireForGroupDto(Guid.Empty, "", "Plain Text", eQuestionnaireUsage.Annotation);
}

