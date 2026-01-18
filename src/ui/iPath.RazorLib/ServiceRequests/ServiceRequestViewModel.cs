using iPath.Application;
using iPath.Application.Contracts;
using iPath.Application.Features.Documents;
using iPath.Blazor.Componenents.ServiceRequests.Annotations;
using iPath.Blazor.Componenents.Shared;
using iPath.Domain.Config;
using Refit;
using System.Linq;

namespace iPath.Blazor.Componenents.ServiceRequests;

public class ServiceRequestViewModel(IPathApi api,
    AppState appState,
    ISnackbar snackbar,
    IDialogService srvDialog,
    IStringLocalizer T,
    NavigationManager nm,
    QuestionnaireCache qCache,
    IOptions<iPathClientConfig> opts,
    ILogger<ServiceRequestViewModel> logger)
    : IViewModel
{
    // Signal State Changes to Views
    public event Action OnChange;
    private void NotifyStateChanged() => OnChange?.Invoke();

    public event Action OnLoadingStarted;
    public event Action OnLoadingFinished;
    public event Action<UploadTask> OnUploadStarted;
    public event Action<UploadTask> OnUploadFinished;

    public string SearchString { get; set; }

    public ServiceRequestDto? SelectedRequest { get; private set; }
    public DocumentDto? SelectedDocument { get; private set; }
    public GroupDto? ActiveGroup { get; private set; }


    public bool IsRootNodeSelected
    {
        get
        {
            if (SelectedRequest is null) return false;
            if (SelectedDocument is null) return true;
            return SelectedDocument.Id == SelectedRequest.Id;
        }
    }


    public void SelectDocument(Guid id)
    {
        SelectDocument(SelectedRequest?.Documents?.FirstOrDefault(c => c.Id == id));
    }

    public void SelectDocument(DocumentDto? doc)
    {
        if (doc is null)
        {
            SelectedDocument = null;
        }
        else if (SelectedRequest is null)
        {
            throw new Exception("cannot select a document node without loading a service request first");
        }
        else if (doc.ServiceRequestId != SelectedRequest.Id)
        {
            throw new Exception("child node is not child of the root node");
        }
        else
        {
            SelectedDocument = doc;
        }
        NotifyStateChanged();
    }


    public void ClearData()
    {
        SelectedRequest = null;
        SelectedDocument = null;
        NotifyStateChanged();
    }

    public async Task LoadNode(Guid id)
    {
        // check if this is just the root or a child node and if so select it
        if (SelectedRequest is not null && SelectedRequest.Id == id)
        {
            // same
            SelectedDocument = null;
        }
        else
        {
            OnLoadingStarted?.Invoke();
            var respN = await api.GetRequestById(id);
            if (respN.IsSuccessful)
            {
                SelectedRequest = respN.Content;

                // load Group
                if (SelectedRequest.GroupId.HasValue && (ActiveGroup is null || ActiveGroup.Id != SelectedRequest.GroupId))
                {
                    var respG = await api.GetGroup(SelectedRequest.GroupId.Value);
                    if (respG.IsSuccessful)
                        ActiveGroup = respG.Content;
                }
            }
            else
            {
                snackbar.AddWarning(respN.ErrorMessage);
                nm.NavigateTo("/");
            }
            OnLoadingFinished?.Invoke();
        }
    }

    public async Task ReloadNode()
    {
        if (SelectedRequest != null)
        {
            var id = SelectedRequest.Id;
            SelectedRequest = null;
            await LoadNode(id);
        }
    }



    public async Task MarkAsVisited()
    {
        if (SelectedRequest is not null)
        {
            await api.UpdateRequestVisit(SelectedRequest.Id);
        }
    }


    #region "-- Navigation --"
    public GetServiceRequestsQuery LastQuery { get; set; }
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
            var cmd = new GetServiceRequestIdListQuery(LastQuery);
            var resp = await api.GetRequestIdList(cmd);
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
        if (SelectedRequest is null) return;

        if (IsRootNodeSelected)
        {
            // go back to nodelist (group, search, mynode)
            nm.NavigateTo(NavUrl);
        }
        else if (SelectedDocument != null && SelectedDocument.ParentNodeId.HasValue)
        {
            // go up to parent
            var tmp = SelectedRequest.Documents.FirstOrDefault(n => n.Id == SelectedDocument.ParentNodeId.Value);
            if (tmp is null)
            {
                SelectedDocument = null;
            }
            else
            {
                SelectedDocument = tmp;
            }
        }
    }

    public async Task GoNext()
    {
        if (SelectedRequest is null) return;

        if (IsRootNodeSelected)
        {
            if (await LoadIdList())
            {
                var idx = IdList.IndexOf(SelectedRequest.Id);
                if (idx < IdList.Count() - 1)
                {
                    // there is one more in list => select
                    var nextId = IdList[idx + 1];
                    ClearData();
                    nm.NavigateTo($"request/{nextId}");
                    return;
                }
            }
            // otherwhise go up
            await GoUp();
        }
        else if (SelectedDocument is not null)
        {
            // find index of current child Node in parents child list
            var list = SelectedRequest.Documents.Where(n => n.ParentNodeId == SelectedDocument.ParentNodeId).OrderBy(n => n.SortNr).ToList();
            var idx = list.IndexOf(SelectedDocument);
            if (idx < list.Count() - 1)
            {
                // there is one more in list => select
                NavigateToDocument(list[idx + 1]);
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
        if (SelectedRequest is null) return;

        if (IsRootNodeSelected)
        {
            // next in list => to be done
            if (await LoadIdList())
            {
                var idx = IdList.IndexOf(SelectedRequest.Id);
                if (idx > 0)
                {
                    // there is one more in list => select
                    var prevId = IdList[idx - 1];
                    ClearData();
                    nm.NavigateTo($"request/{prevId}");
                    return;
                }
            }
            // otherwhise go up
            await GoUp();
        }
        else
        {
            // find index of current child Node in parents child list
            var list = SelectedRequest.Documents.Where(n => n.ParentNodeId == SelectedDocument.ParentNodeId).OrderBy(n => n.SortNr).ToList();
            var idx = list.IndexOf(SelectedDocument);
            if (idx > 0)
            {
                // there is one more in list => select
                NavigateToDocument(list[idx - 1]);
            }
            else
            {
                // otherwhise go up
                await GoUp();
            }
        }
    }


    public void NavigateToDocument(DocumentDto childNode)
    {
        // nm.NavigateTo($"request/{childNode.Id}");
        SelectDocument(childNode);
    }




    public async Task SelectNextImage()
    {
        if (SelectedRequest is null) return;

        if (SelectedDocument is not null)
        {
            // find index of current child Node
            var list = SelectedRequest.Documents.Where(n => n.IsImage).OrderBy(n => n.SortNr).ToList();
            if (list.IsEmpty()) await GoUp();
            var idx = list.IndexOf(SelectedDocument);
            if (idx < list.Count() - 1)
            {
                // there is one more in list => select
                SelectedDocument = list[idx + 1];
            }
            else
            {
                // goto first
                SelectedDocument = list.First();
            }
        }
    }

    public async Task SelectPreviousImage()
    {
        if (SelectedRequest is null) return;

        if (SelectedDocument is not null)
        {
            // find index of current child Node
            var list = SelectedRequest.Documents.Where(n => n.IsImage).OrderBy(n => n.SortNr).ToList();
            if (list.IsEmpty()) await GoUp();
            var idx = list.IndexOf(SelectedDocument);
            if (idx > 0)
            {
                // there is one more in list => select
                SelectedDocument = list[idx - 1];
            }
            else
            {
                // goto last
                SelectedDocument = list.Last();
            }
        }
    }
    #endregion


    #region  "-- Actions --"


    public bool SortingDisabled = false;

    public async Task SaveChildNodeSortOrder(Dictionary<Guid, int> sortOrder)
    {
        if (SelectedRequest != null)
        {
            var cmd = new UpdateDocumentsSortOrderCommand(SelectedRequest.Id, sortOrder);
            var resp = await api.UpdateDocumentsSortOrder(cmd);
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
        var resp = await api.CreateRequest(new CreateServiceRequestCommand(GroupId: GroupId, RequestType: "Case"));
        if (resp.IsSuccessful)
        {
            SelectedRequest = resp.Content;
            if (!string.IsNullOrEmpty(QuestionaireId))
            {
                SelectedRequest.Description.Questionnaire = new QuestionnaireResponseData { QuestionnaireId = QuestionaireId };
            }
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

    public async Task Delete(bool AskConfirmation = true)
    {
        // Delete root node if none îs selected
        if (SelectedDocument is null)
        {
            if (AskConfirmation)
            {
                bool? result = await srvDialog.ShowMessageBox(
                    T["Warning"],
                    T["Are you sure that you want to delete !"],
                    yesText: T["Yes"], cancelText: T["Cancel"]);
                if (result is null)
                    return;
            }
            if (SelectedRequest != null)
            {
                var resp = await api.DeleteRequest(SelectedRequest.Id);
                if (resp.IsSuccessful)
                {
                        IsEditing = false;
                        await GoUp();
                }
                else
                {
                    snackbar.AddError(resp.ErrorMessage);
                }
            }
        }
    }

    public async Task DeleteDcouments(HashSet<Guid> ids, bool AskConfirmation = true)
    {
        if (SelectedRequest is null) return;

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
            var doc = SelectedRequest.Documents.FirstOrDefault(n => n.Id == id);
            if (doc != null)
            {
                var resp = await api.DeleteDocument(doc.Id);
                if (resp.IsSuccessful)
                {
                    SelectedRequest.Documents.Remove(doc);
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
            snackbar.Add(T["Documents deleted"], Severity.Success);
        }

        NotifyStateChanged();
    }


    public bool IsEditing
    {
        get;
        set { field = value; NotifyStateChanged(); }
    }

    public bool EditDisabled => !appState.CanEditNode(SelectedRequest);

    public async Task Edit(DocumentDto? node = null)
    {
        if (!EditDisabled && SelectedRequest is not null)
        {
            nm.NavigateTo($"request/edit/{SelectedRequest.Id}");
        }
    }


    public bool SaveDisabled => !IsEditing;

    public delegate Task BeforeSaveEventHandler(object? sender);
    public event BeforeSaveEventHandler? OnBeforeSaveEvent;

    public async Task Save()
    {
        if (OnBeforeSaveEvent is not null)
            await OnBeforeSaveEvent.Invoke(this);

        if (SelectedRequest != null && !SaveDisabled && IsEditing)
        {
            var cmd = new UpdateServiceRequestCommand(NodeId: SelectedRequest.Id, Description: SelectedRequest.Description, IsDraft: false);
            var resp = await api.UpdateRequest(cmd);
            if (!resp.IsSuccessful)
            {
                snackbar.AddError(resp.ErrorMessage);
            }
            else
            {
                nm.NavigateTo($"request/{SelectedRequest.Id}");
            }
            IsEditing = false;

            OnChange();
        }
    }

    public async Task CancelEdit()
    {
        if (SelectedRequest != null)
        {
            IsEditing = false;

            if (SelectedRequest.IsDraft)
            {
                IApiResponse resp;

                // check if there is no valid input and no child nodes => delete
                if (SelectedRequest.Documents.IsEmpty() && !SelectedRequest.Description.ValidateInput())
                {
                    resp = await api.DeleteRequest(SelectedRequest.Id);
                }
                else
                {
                    // when cancelling a draft, save current state ...
                    var cmd = new UpdateServiceRequestCommand(NodeId: SelectedRequest.Id, Description: SelectedRequest.Description, IsDraft: true);
                    resp = await api.UpdateRequest(cmd);
                }

                if (resp.IsSuccessful)
                {
                    // and go up
                    await GoUp();
                }
                else
                {
                    snackbar.AddError(resp.ErrorMessage);
                }
            }
            else
            {
                nm.NavigateTo($"request/{SelectedRequest.Id}");
            }
        }
    }



    public bool AttachFileDisabled => EditDisabled;

    public UploadTask CreateUploadTask() => new UploadTask(api, opts.Value.MaxFileSizeBytes);

    public async Task UploadFile(IBrowserFile f)
    {
        try
        {
            if (SelectedRequest is null)
            {
                snackbar.AddWarning("no service request selected");
            }
            else if (f.Size > opts.Value.MaxFileSizeBytes)
            {
                snackbar.Add("File is larger then " + opts.Value.MaxFileSize);
            }
            else
            {
                logger.LogInformation("starting file upload: " + f.Name);

                var t = CreateUploadTask();
                OnUploadStarted?.Invoke(t);

                await t.Upload(f, SelectedRequest.Id, SelectedDocument?.Id);

                //var stream = new StreamPart(f.OpenReadStream(maxAllowedSize: IPathApi.MaxFileSize), f.Name, f.ContentType);
                //var resp = await api.UploadNodeFile(stream, SelectedNode.Id);
                if (t.IsSuccessful)
                {
                    // append to child nodes
                    SelectedRequest.Documents.Add(t.Result);
                    OnChange();
                }
                else
                {
                    snackbar.AddWarning(t.Error);
                }

                OnUploadFinished?.Invoke(t);
                OnChange?.Invoke();
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
        if (SelectedRequest is not null)
        {
            model.RootNodeId = SelectedRequest.Id;
            model.ChildNodeId = ChildNodeId;
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
            var cmd = new CreateAnnotationCommand(NewAnnotation.RootNodeId, NewAnnotation.Data, NewAnnotation.ChildNodeId);

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



    public bool HasImages => SelectedRequest is not null && SelectedRequest.Documents.Any(n => n.IsImage);
    public void StartSlideshow()
    {
        if (SelectedRequest is not null)
        {
            nm.NavigateTo($"request/{SelectedRequest.Id}/slideshow");
        }
    }

    #endregion


    public async Task<string?> GetQuesiotnnaire(RequestDescription model)
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




    public async Task DeleteDocument(DocumentDto document)
    {
        throw new NotImplementedException();
    }

    public async Task EditDocument(DocumentDto document)
    {
        throw new NotImplementedException();
    }
}

