namespace iPath.Domain.Entities;

public class GroupSettings
{
    public string Purpose { get; set; } = "";

    public bool DescriptionAllowHtml { get; set; } = true;
    public string DescriptionTemplate { get; set; } = "";
    public bool DescriptionWithBodySite { get; set; } = true;

    public bool UseDescriptionWizzard { get; set; }

    public bool AnnotationsHide { get; set; } = false;
    public bool AnnotationHasMoprhoogy { get; set; } = true;

    public bool UseCaseTitleField { get; set; } = true;
    public bool UseCaseSubTitleField { get; set; } = true;

    public bool UseCaseTypeField { 
        get; 
        set => field = value; 
    }
    
    public ICollection<string> CaseTypes { get; set; } = [];

    public ICollection<eAnnotationType> AllowedAnnotationTypes { get; set; } = [ eAnnotationType.Comment, eAnnotationType.FinalAssesment, eAnnotationType.FollowUp ];


    public StorageInfo? Storage { get; set; }

    public GroupSettings Clone() => (GroupSettings)MemberwiseClone();
}
