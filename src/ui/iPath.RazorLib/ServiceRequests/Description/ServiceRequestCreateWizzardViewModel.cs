using Hl7.Fhir.Utility;
using iPath.Application;
using iPath.Application.Fhir;
using iPath.LHCForms;
using Microsoft.Extensions.DependencyInjection;

namespace iPath.Blazor.Componenents.ServiceRequests;

public class ServiceRequestCreateWizzardViewModel(IServiceProvider sp, ServiceRequestViewModel vm)
{
    private CodingService _coding;
    private IFhirDataLoader _loader;
    private QuestionnaireCacheClient _cache;

    public string CodingService { get; private set; }

    public async Task InitializeAsync(string CodingService, string ValueSetId, CancellationToken ct = default)
    {
        this.CodingService = CodingService;
        _coding = sp.GetRequiredKeyedService<CodingService>(CodingService);
        _loader = sp.GetRequiredService<IFhirDataLoader>();
        _cache = sp.GetRequiredService<QuestionnaireCacheClient>();

        await _coding.LoadCodeSystem();
        await _coding.LoadValueSet(ValueSetId);
        var vs = _coding.GetValueSetDisplay(ValueSetId);
        var r = vs.DisplayTree;

        if (r.Count == 1)
        {
            RootCodes = r.First().Children;
        }
        else
        {
            RootCodes = r;
        }
    }

    // shortcut to RequestDescription
    public RequestDescription Data => vm.SelectedRequest.Description;


    public IEnumerable<CodeDisplay> RootCodes { get; private set; }


    public CodeDisplay Organ
    {
        get => field;
        set
        {
            field = value;
            BodySiteCodes = Organ is null ? new List<TreeItemData<CodeDisplay>>() : Organ.Children.ToTreeView();
            if (BodySiteCodes.Count == 1)
            {
                BodySiteCodes.First().Expanded = true;
            }
        }
    }

    public List<TreeItemData<CodeDisplay>> BodySiteCodes { get; private set; } = new();
    public bool OrganGroupExpanded => Organ != null && Organ.Children != null && Organ.Children.Count == 1;

    public bool TopoAutoSelect { get; set; }

    public CodeDisplay Topo
    {
        get => field;
        set
        {
            field = value;
            Data.BodySite = value.ToConcept(_coding.CodeSystemUrl);
        }
    }

    public bool SaveAsDraft { get; set; } = false;




    #region "-- Questionnaire Handling --"

    public IQuestionnaireForm QuestionnaireViewer { get; set; }

    public IReadOnlyCollection<QuestionnaireForGroupDto> validForms { get; private set; } = new List<QuestionnaireForGroupDto>();

    public QuestionnaireForGroupDto? SelectedQ { get; set; }


    public async Task LoadQuestionnaire()
    {
        SelectedQ = null;
        validForms = new List<QuestionnaireForGroupDto>();
        if (Data.BodySite != null)
        {
            // find all questionnaires that are valid for Usage as CaseDescription and that match the body site
            validForms = await vm.ActiveGroup.Questionnaires.FilterAsync(eQuestionnaireUsage.CaseDescription, Data.BodySite.Code);

            // autoselect first form if only 1 is valid
            if (validForms.Count > 0)
            {
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
        if (QuestionnaireViewer != null)
        {
            Data.Questionnaire.Resource = await QuestionnaireViewer.GetDataAsync();
        }
    }

    #endregion


    public bool NeedFullTopo { get; set; }



    public string TopoText
    {
        get
        {
            if (Topo is not null)
            {
                return Topo.ToDisplay();
            }
            else if (Organ is not null && !NeedFullTopo)
            {
                return Organ.ToDisplay();
            }
            else
            {
                return "Select body site";
            }
        }
    }

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




    public async Task OnPreviewInteraction(StepperInteractionEventArgs arg)
    {
        if (arg.Action == StepAction.Complete)
        {
            // occurrs when clicking next
            await ControlStepCompletion(arg);
        }
        else if (arg.Action == StepAction.Activate)
        {
            // occurrs when clicking a step header with the mouse
            await ControlStepNavigation(arg);
        }
        else if (arg.Action == StepAction.Reset)
        {
            vm.ResetRequest();
        }
    }

    private async Task ControlStepCompletion(StepperInteractionEventArgs arg)
    {
        switch (arg.StepIndex)
        {
            case 0:
                Step1Complete = NeedFullTopo ? (Topo is not null) : (Organ is not null);
                arg.Cancel = !Step1Complete.Value;

                if (Step1Complete.Value)
                {
                    await vm.SaveDraft(true);
                    await LoadQuestionnaire();
                }

                break;

            case 1:
                await SaveQuestionnare();

                Step2Complete = Data.IsClinicalInfoValid;
                arg.Cancel = !Step2Complete.Value;

                if (Step2Complete.Value)
                {
                    await vm.SaveDraft(true);
                }

                break;

            case 2:
                await vm.SaveDraft(SaveAsDraft);
                // tell the ViewModel that we are done editing. This prevents saving the request as draft on leaving the page.
                vm.IsEditing = false;

                OnComplete?.Invoke();
                break;
        }
    }

    private async Task ControlStepNavigation(StepperInteractionEventArgs arg)
    {
        switch (arg.StepIndex)
        {
            case 1:
                // ensure topo (step 1) is complete
                if (Step1Complete != true) arg.Cancel = true;

                // load Questionnaire it require
                await LoadQuestionnaire();

                break;

            case 2:
                // ensure pqtient data (step 2) is complete
                if (Step2Complete != true) arg.Cancel = true;

                // read questionnaire data
                await SaveQuestionnare();

                break;
        }
    }


    public int ActiveStepIndex { get; set; }
    public Action OnComplete { get; set; }

    public bool? Step1Complete { get; private set; }
    public bool? Step2Complete { get; private set; }
}
