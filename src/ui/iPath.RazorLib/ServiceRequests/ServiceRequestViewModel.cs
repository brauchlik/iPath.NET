using Hl7.Fhir.Model;
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
    QuestionnaireCacheClient qCache,
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

    public ServiceRequestDto? SelectedRequest { 
        get; 
        private set
        {
            RequestOwner = value?.Owner;
            field = value;
        }
    }
    public OwnerDto? RequestOwner { get; set; } // May be modified by view

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


    public List<DocumentDto> GetVisibleDocuments(DocumentDto? parent = null)
    {
        if (SelectedRequest is not null)
        {
            // ToDo: Filter by parent document
            return SelectedRequest.Documents
                .Where(d => !d.ParentNodeId.HasValue)
                .OrderBy(d => d.SortNr)
                .ToList();
        }
        return new();
    }



    public void ClearData()
    {
        SelectedRequest = null;
        SelectedDocument = null;
        RequestOwner = null;
        NotifyStateChanged();
    }


    public bool IsRequestLoaded(string id)
    {
        if (Guid.TryParse(id, out var guid) && SelectedRequest is not null)
        {
            return (guid == SelectedRequest.Id);
        }
        return false;
    }

    public async Task LoadNode(Guid id)
    {
        ClearData();

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


    public bool HasDocument(Guid? id)
    {
        if (id.HasValue && SelectedRequest is not null) 
        { 
            return SelectedRequest.Documents.Any(d => d.Id == id.Value);
        }
        return false;
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


    public async Task GoUpRequestPage()
    {
        if (SelectedRequest is null)
        {
            nm.NavigateTo(NavUrl);
        }
        else
        {
            nm.NavigateTo($"request/{SelectedRequest.Id}");
        }
    }

    public async Task GoUp()
    {
        if (SelectedRequest is null) return;

        if (IsRootNodeSelected)
        {
            // go back to nodelist (group, search, mynode)
            nm.NavigateTo(NavUrl);
        }
        else if (SelectedDocument != null)
        {
            if (SelectedDocument.ParentNodeId.HasValue)
            {
                SelectDocument(SelectedDocument.ParentNodeId.Value);
            }
            else
            {
                SelectDocument(null);
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




    public async Task SelectNextSlide()
    {
        if (SelectedRequest is null) return;

        if (SelectedDocument is not null)
        {
            // find index of current child Node
            var list = SelectedRequest.Documents.Where(n => n.IsSlide).OrderBy(n => n.SortNr).ToList();
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

    public async Task SelectPreviousSlide()
    {
        if (SelectedRequest is null) return;

        if (SelectedDocument is not null)
        {
            // find index of current child Node
            var list = SelectedRequest.Documents.Where(n => n.IsSlide).OrderBy(n => n.SortNr).ToList();
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


    public bool IsModerator => ActiveGroup is not null && appState.IsGroupModerator(ActiveGroup.Id);
    public bool DisableSenderChange => !IsModerator; // could maybe change to admin only


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
                bool? result = await srvDialog.ShowMessageBoxAsync(
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
        else
        {
            await DeleteDocument(SelectedDocument, AskConfirmation);
            // await GoUp();
        }
    }


    public async Task DeleteDocument(DocumentDto document, bool AskConfirmation = true)
    {
        if (AskConfirmation)
        {
            bool? result = await srvDialog.ShowMessageBoxAsync(
            T["Warning"],
            string.Format(T["Are you sure that you want to delete the document {0}"], document.GalleryCaption),
            yesText: T["Yes"], cancelText: T["Cancel"]);
            if (result is null)
                return;
        }
        await DeleteDocuments(new HashSet<Guid> { document.Id }, false);
    }

    public async Task DeleteDocuments(HashSet<Guid> ids, bool AskConfirmation = true)
    {
        if (SelectedRequest is null) return;

        if (AskConfirmation)
        {
            bool? result = await srvDialog.ShowMessageBoxAsync(
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

    public async Task Save(bool finishEditing = true)
    {
        if (OnBeforeSaveEvent is not null)
            await OnBeforeSaveEvent.Invoke(this);

        if (SelectedRequest != null && !SaveDisabled && IsEditing)
        {
            Guid? newOwnerId = SelectedRequest.OwnerId != RequestOwner?.Id ? RequestOwner.Id : null;

            var cmd = new UpdateServiceRequestCommand(NodeId: SelectedRequest.Id, Description: SelectedRequest.Description, NewOwnerId: newOwnerId, IsDraft: false);
            var resp = await api.UpdateRequest(cmd);
            if (!resp.IsSuccessful)
            {
                snackbar.AddError(resp.ErrorMessage);
                return;
            }
            NotifyStateChanged();

            if (finishEditing)
            {
                IsEditing = false;
                nm.NavigateTo($"request/{SelectedRequest.Id}");
            }
        }
    }


    public async Task SaveDraft(bool isDraft)
    {
        if (OnBeforeSaveEvent is not null)
            await OnBeforeSaveEvent.Invoke(this);

        if (SelectedRequest != null && !SaveDisabled && IsEditing)
        {
            Guid? newOwnerId = SelectedRequest.OwnerId != RequestOwner?.Id ? RequestOwner.Id : null;

            var cmd = new UpdateServiceRequestCommand(NodeId: SelectedRequest.Id, Description: SelectedRequest.Description, NewOwnerId: newOwnerId, IsDraft: isDraft);
            var resp = await api.UpdateRequest(cmd);
            if (!resp.IsSuccessful)
            {
                snackbar.AddError(resp.ErrorMessage);
            }
            NotifyStateChanged();
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
                    var cmd = new UpdateServiceRequestCommand(NodeId: SelectedRequest.Id, Description: SelectedRequest.Description);
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
                    NotifyStateChanged();
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

    public async Task Annotate(DocumentDto document = null, bool ReloadOnSave = true)
    {
        if (AnnotateDisabled) return;

        var dlgTitle = T["New Comment"].ToString();
        if (document is not null)
            dlgTitle = string.Format(T["New Comment on {0}"], document.GalleryCaption.ShortenTo(25));

        var model = CreateNewAnnotationInput(document);
        var parameters = new DialogParameters<AddPlaintextAnnotationDialog> { { x => x.Model, model } };
        var dialog = await srvDialog.ShowAsync<AddPlaintextAnnotationDialog>(dlgTitle, parameters);
        var result = await dialog.Result;
        if (!result.Canceled && result.Data is AnnotationEditModel data)
        {
            if (data.Data.ValidateInput())
            {
                await SubmitAnnotation(data, ReloadOnSave);
            }
        }
    }

    public AnnotationEditModel? NewAnnotation { get; set; }
    public async Task StartAnnotation()
    {
        NewAnnotation = CreateNewAnnotationInput();
        OnChange?.Invoke();
    }

    private AnnotationEditModel CreateNewAnnotationInput(DocumentDto? Document = null)
    {
        var model = new AnnotationEditModel();
        if (SelectedRequest is not null)
        {
            model.ServiceRequestId = SelectedRequest.Id;
            model.DocumentId = Document?.Id;
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

    public async Task SubmitAnnotation(AnnotationEditModel? data = null, bool ReloadOnSave = true)
    {
        data ??= NewAnnotation;
        if (data is not null)
        {
            var cmd = new CreateAnnotationCommand(data.ServiceRequestId, data.Data, data.DocumentId);

            var resp = await api.CreateAnnotation(cmd);
            if (resp.IsSuccessful)
            {
                NewAnnotation = null;
                if (ReloadOnSave)
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
    public bool HasWSI => SelectedRequest is not null && SelectedRequest.Documents.Any(n => n.IsWSI);

    public bool HasSlideShow => SelectedRequest is not null && SelectedRequest.Documents.Any(n => n.IsSlide);

    public string SlideShowUrl => SelectedRequest is null ? "" : $"request/{SelectedRequest?.Id}/slideshow";
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



    public async Task EditDocument(DocumentDto document)
    {
        throw new NotImplementedException();
    }

    internal void ResetRequest()
    {
        if (SelectedRequest is not null && SelectedRequest.IsDraft)
        {
            SelectedRequest.ResetDescription();
        }
    }
}

