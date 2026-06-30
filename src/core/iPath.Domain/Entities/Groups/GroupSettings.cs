namespace iPath.Domain.Entities;

public class GroupSettings
{
    public string Purpose { get; set; } = "";

    public bool DescriptionAllowHtml { get; set; } = true;
    public string DescriptionTemplate { get; set; } = "";
    public bool DescriptionWithBodySite { get; set; } = true;

    public bool UseDescriptionWizzard { get; set; }
    public bool ShowProvisionalDiagnosis { get; set; } = false;

    public bool AnnotationsHide { get; set; } = false;
    public bool AnnotationHasMoprhoogy { get; set; } = true;

    public bool UseCaseTitleField { get; set; } = true;
    public bool UseCaseSubTitleField { get; set; } = true;

    public bool UseCaseTypeField { 
        get; 
        set => field = value; 
    }
    
    private ICollection<string> _caseTypes = [];
    public ICollection<string> CaseTypes
    {
        get => _caseTypes ??= [];
        set => _caseTypes = value ?? [];
    }

    public ICollection<eAnnotationType> AllowedAnnotationTypes { get; set; } = [ eAnnotationType.Comment, eAnnotationType.FinalAssesment, eAnnotationType.FollowUp ];

    public string? TopographyValueSet { get; set; }
    public string? MorphologyValueSet { get; set; }


    public StorageInfo? Storage { get; set; }

    public eTaskAssignmentStrategy TaskAssignmentStrategy { get; set; } = eTaskAssignmentStrategy.None;
    public int? AutoAssignTimeoutHours { get; set; } = 24;

    public AiConfig AiSettings = new();

    public GroupSettings Clone() => (GroupSettings)MemberwiseClone();
}
