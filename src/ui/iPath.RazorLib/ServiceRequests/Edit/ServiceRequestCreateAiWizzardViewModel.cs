using iPath.Application;
using iPath.Application.AI;
using iPath.Application.Fhir;
using iPath.Domain.Entities;
using iPath.LHCForms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace iPath.Blazor.Componenents.ServiceRequests;

public class ServiceRequestCreateAiWizzardViewModel
{
    private readonly IServiceProvider _sp;
    private readonly ServiceRequestViewModel _vm;
    private CodingService _coding;
    private IFhirDataLoader _loader;
    private QuestionnaireCacheClient _cache;
    private IAiExtractionService _extractionService;
    private ISemanticSearchService _semanticSearchService;
    private ILogger<ServiceRequestCreateAiWizzardViewModel> _logger;

    public string CodingService { get; private set; }
    public Action OnInitialized { get; set; }
    public Action OnComplete { get; set; }

    public string RawIntakeText { get; set; } = string.Empty;
    public bool IsAnalyzing { get; set; } = false;
    public AiExtractionResult? ExtractionResult { get; set; }
    public bool Step0Complete { get; set; } = false;
    public bool? Step1Complete { get; set; }
    public bool? Step2Complete { get; set; }

    // Backup of original AI suggestion to compute delta corrections
    public string OriginalAiTopographyCode { get; set; } = string.Empty;
    public string OriginalAiSex { get; set; } = string.Empty;
    public int? OriginalAiAge { get; set; }

    public ServiceRequestCreateAiWizzardViewModel(IServiceProvider sp, ServiceRequestViewModel vm)
    {
        _sp = sp;
        _vm = vm;
        _logger = sp.GetRequiredService<ILogger<ServiceRequestCreateAiWizzardViewModel>>();
    }

    public async Task InitializeAsync(string codingServiceKey, string valueSetId, CancellationToken ct = default)
    {
        try
        {
            this.CodingService = codingServiceKey;
            _coding = _sp.GetRequiredKeyedService<CodingService>(codingServiceKey);
            _loader = _sp.GetRequiredService<IFhirDataLoader>();
            _cache = _sp.GetRequiredService<QuestionnaireCacheClient>();
            _extractionService = _sp.GetRequiredService<IAiExtractionService>();
            _semanticSearchService = _sp.GetRequiredService<ISemanticSearchService>();

            await _coding.LoadCodeSystem();
            await _coding.LoadValueSet(valueSetId);
            var vs = _coding.GetValueSetDisplay(valueSetId);
            var r = vs.DisplayTree;

            if (r.Count == 1)
            {
                RootCodes = r.First().Children;
            }
            else
            {
                RootCodes = r;
            }

            OnInitialized?.Invoke();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize AI Wizard view model");
            throw;
        }
    }

    public IEnumerable<CodeDisplay> RootCodes { get; private set; }

    private CodeDisplay _organ;
    public CodeDisplay Organ
    {
        get => _organ;
        set
        {
            _organ = value;
            BodySiteCodes = _organ is null ? new List<TreeItemData<CodeDisplay>>() : _organ.Children.ToTreeView();
            if (BodySiteCodes.Count == 1)
            {
                BodySiteCodes.First().Expanded = true;
            }
        }
    }

    public List<TreeItemData<CodeDisplay>> BodySiteCodes { get; private set; } = new();
    
    public bool TopoAutoSelect { get; set; }

    private CodeDisplay _topo;
    public CodeDisplay Topo
    {
        get => _topo;
        set
        {
            _topo = value;
            if (_topo is not null)
            {
                Data.BodySite = _topo.ToConcept(_coding.CodeSystemUrl);
            }
        }
    }

    public RequestDescription Data => _vm.SelectedRequest.Description;

    public bool ShowCaseTitle => _vm.ActiveGroup?.Settings is not null && _vm.ActiveGroup.Settings.UseCaseTitleField;
    public bool ShowCaseSubTitle => _vm.ActiveGroup?.Settings is not null && _vm.ActiveGroup.Settings.UseCaseSubTitleField;
    public bool ShowProvisionalDiagnosis => _vm.ActiveGroup?.Settings is not null && _vm.ActiveGroup.Settings.ShowProvisionalDiagnosis;
    public Guid? GroupId => _vm.ActiveGroup?.Id;
    public bool CaseTypeActive => _vm.ActiveGroup.CaseTypeActive;
    public bool SaveAsDraft { get; set; } = false;

    public IReadOnlyCollection<string> CaseTypes
    {
        get
        {
            if (_vm.ActiveGroup is not null && _vm.ActiveGroup.Settings.UseCaseTypeField)
            {
                return _vm.ActiveGroup.Settings.CaseTypes.ToList();
            }
            return new List<string>();
        }
    }

    public async Task AnalyzeClinicalHistoryAsync()
    {
        if (string.IsNullOrWhiteSpace(RawIntakeText)) return;

        IsAnalyzing = true;
        try
        {
            // Set the clinical history text directly
            Data.Text = RawIntakeText;

            // Trigger AI Extraction Service
            ExtractionResult = await _extractionService.ExtractAsync(
                RawIntakeText,
                _vm.ActiveGroup?.Community?.Id,
                _vm.ActiveGroup?.Id,
                _vm.SelectedRequest?.OwnerId
            );

            // Populate form fields with extracted details
            if (ExtractionResult != null)
            {
                if (ExtractionResult.Age.HasValue)
                {
                    Data.PatientInfo.Age = ExtractionResult.Age.Value;
                    OriginalAiAge = ExtractionResult.Age;
                }

                if (!string.IsNullOrWhiteSpace(ExtractionResult.Sex))
                {
                    Data.PatientInfo.Gender = ExtractionResult.Sex;
                    OriginalAiSex = ExtractionResult.Sex;
                }

                if (!string.IsNullOrWhiteSpace(ExtractionResult.TopographyCode) && ExtractionResult.IsTopographyValid)
                {
                    var concept = new CodeDisplay
                    {
                        Code = ExtractionResult.TopographyCode,
                        Display = ExtractionResult.TopographyName ?? ExtractionResult.TopographyCode
                    };
                    Data.BodySite = concept.ToConcept(_coding.CodeSystemUrl);
                    OriginalAiTopographyCode = ExtractionResult.TopographyCode;
                }

                // If clinical questions are extracted, join them to Provisional Diagnosis or add to description
                if (ExtractionResult.ClinicalQuestions != null && ExtractionResult.ClinicalQuestions.Count > 0)
                {
                    Data.ProvisionalDiagnosis = string.Join("; ", ExtractionResult.ClinicalQuestions);
                }

                // Silently run Semantic Search indexing in background
                if (_vm.SelectedRequest != null)
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await _semanticSearchService.SaveEmbeddingAsync(_vm.SelectedRequest.Id, RawIntakeText);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Background semantic search indexing failed");
                        }
                    });
                }
            }

            Step0Complete = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during AI clinical text analysis");
        }
        finally
        {
            IsAnalyzing = false;
        }
    }

    #region "-- Questionnaire Handling --"

    public IQuestionnaireForm QuestionnaireViewer { get; set; }
    public IReadOnlyCollection<QuestionnaireForGroupDto> validForms { get; private set; } = new List<QuestionnaireForGroupDto>();
    public bool UseQuestionnaire { get; set; }
    public QuestionnaireForGroupDto? SelectedQ { get; set; }

    public async Task LoadQuestionnaire()
    {
        UseQuestionnaire = false;
        SelectedQ = null;
        validForms = new List<QuestionnaireForGroupDto>();
        if (Data.BodySite != null)
        {
            validForms = (await _vm.ActiveGroup.Questionnaires
                .OrderBy(f => f.Priority)
                .FilterAsync(eQuestionnaireUsage.CaseDescription, Data.BodySite.Code));

            if (validForms.Count > 0)
            {
                UseQuestionnaire = true;
                SelectedQ = validForms.First();
                Data.Questionnaire = new QuestionnaireResponseData { QuestionnaireId = SelectedQ.QuestinnaireId };
            }
        }
        await LoadQuestionnaireForm();
    }

    public async Task LoadQuestionnaireForm()
    {
        if (SelectedQ is not null && QuestionnaireViewer is not null)
        {
            var q = await _cache.GetQuestionnaireResourceAsync(SelectedQ.QuestinnaireId);
            await QuestionnaireViewer.LoadFormAsync(q, Data.Questionnaire?.Resource!);
        }
    }

    async Task SaveQuestionnare()
    {
        if (UseQuestionnaire)
        {
            if (SelectedQ != null && QuestionnaireViewer != null)
            {
                Data.Questionnaire.QuestionnaireId = SelectedQ.QuestinnaireId;
                Data.Questionnaire.Resource = await QuestionnaireViewer.GetDataAsync();
            }
        }
        else
        {
            Data.Questionnaire = null;
        }
    }

    #endregion

    public string TopoText => Data.BodySite != null ? $"{Data.BodySite.Code} - {Data.BodySite.Display}" : "Select body site";

    public string PatientText
    {
        get
        {
            string ret = "";
            if (!string.IsNullOrEmpty(Data.AccessionNo))
            {
                ret = Data.AccessionNo;
            }
            if (!string.IsNullOrEmpty(Data.PatientInfo.Gender))
            {
                ret = ret.Append(Data.PatientInfo.Gender);
            }
            if (Data.PatientInfo.Age.HasValue)
            {
                ret = ret.Append($"{Data.PatientInfo.Age} years");
            }

            return Data.IsClinicalInfoValid ? ret : "Patient Information";
        }
    }

    public int ActiveStepIndex { get; set; }

    public async Task OnPreviewInteraction(StepperInteractionEventArgs arg)
    {
        if (arg.Action == StepAction.Complete)
        {
            await ControlStepCompletion(arg);
        }
        else if (arg.Action == StepAction.Activate)
        {
            await ControlStepNavigation(arg);
        }
        else if (arg.Action == StepAction.Reset)
        {
            _vm.ResetRequest();
            RawIntakeText = string.Empty;
            Step0Complete = false;
            ExtractionResult = null;
        }
    }

    private async Task ControlStepCompletion(StepperInteractionEventArgs arg)
    {
        switch (arg.StepIndex)
        {
            case 0:
                // Intake Screen
                if (!Step0Complete)
                {
                    await AnalyzeClinicalHistoryAsync();
                }
                arg.Cancel = !Step0Complete;
                if (Step0Complete)
                {
                    await _vm.SaveDraft(true);
                    await LoadQuestionnaire();
                }
                break;

            case 1:
                // Verification Screen
                Step1Complete = Data.BodySite is not null;
                arg.Cancel = !Step1Complete.Value;

                if (Step1Complete.Value)
                {
                    await _vm.SaveDraft(true);
                    await LoadQuestionnaire();
                }
                break;

            case 2:
                // Patient Details Screen
                await SaveQuestionnare();
                Step2Complete = Data.IsClinicalInfoValid;
                arg.Cancel = !Step2Complete.Value;

                if (Step2Complete.Value)
                {
                    // Compute & save AI correction deltas if fields were overridden
                    await SaveCorrectionDeltasAsync();

                    await _vm.SaveDraft(true);
                }
                break;

            case 3:
                // Image Upload & Save
                await _vm.SaveDraft(SaveAsDraft);
                _vm.IsEditing = false;
                OnComplete?.Invoke();
                break;
        }
    }

    private async Task ControlStepNavigation(StepperInteractionEventArgs arg)
    {
        switch (arg.StepIndex)
        {
            case 1:
                if (!Step0Complete) arg.Cancel = true;
                break;

            case 2:
                if (Step1Complete != true) arg.Cancel = true;
                await LoadQuestionnaire();
                break;

            case 3:
                if (Step2Complete != true) arg.Cancel = true;
                await SaveQuestionnare();
                break;
        }
    }

    private async Task SaveCorrectionDeltasAsync()
    {
        try
        {
            if (_vm.SelectedRequest != null && ExtractionResult != null)
            {
                var humanAccepted = new
                {
                    Age = Data.PatientInfo.Age,
                    Sex = Data.PatientInfo.Gender,
                    TopographyCode = Data.BodySite?.Code,
                    TopographyName = Data.BodySite?.Display
                };

                string humanAcceptedJson = System.Text.Json.JsonSerializer.Serialize(humanAccepted);
                bool wasOverridden = OriginalAiTopographyCode != Data.BodySite?.Code ||
                                    OriginalAiSex != Data.PatientInfo.Gender ||
                                    OriginalAiAge != Data.PatientInfo.Age;

                await _extractionService.SaveIngestionLineageAsync(
                    _vm.SelectedRequest.Id,
                    GroupId,
                    RawIntakeText,
                    ExtractionResult.RawSuggestedJson,
                    humanAcceptedJson,
                    ExtractionResult.ModelUsed,
                    wasOverridden
                );

                if (OriginalAiTopographyCode != Data.BodySite?.Code)
                {
                    await _extractionService.SaveCorrectionDeltaAsync(
                        GroupId,
                        "TopographyCode",
                        OriginalAiTopographyCode,
                        Data.BodySite?.Code,
                        ExtractionResult.Snippet
                    );
                }

                if (OriginalAiSex != Data.PatientInfo.Gender)
                {
                    await _extractionService.SaveCorrectionDeltaAsync(
                        GroupId,
                        "Sex",
                        OriginalAiSex,
                        Data.PatientInfo.Gender,
                        null
                    );
                }

                if (OriginalAiAge != Data.PatientInfo.Age)
                {
                    await _extractionService.SaveCorrectionDeltaAsync(
                        GroupId,
                        "Age",
                        OriginalAiAge?.ToString(),
                        Data.PatientInfo.Age?.ToString(),
                        null
                    );
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save AI ingestion lineage and correction deltas");
        }
    }
}
