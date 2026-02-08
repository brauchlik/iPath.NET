namespace iPath.Domain.Entities;

public class RequestDescription
{
    public StorageInfo? Storage { get; set; }

    public string? Subtitle { get; set; }
    public string? CaseType { get; set; }
    public string? AccessionNo { get; set; }
    public string? Status { get; set; }

    [Required, MinLength(3)]
    public string? Title { get; set; } = string.Empty!;
    public string? Text { get; set; } = string.Empty!;

    public PatientInfo PatientInfo { get; set; } = new();

    public QuestionnaireResponseData? Questionnaire { get; set; }

    public CodedConcept? BodySite { get; set; }

    public RequestDescription Clone() => (RequestDescription)MemberwiseClone();


    public bool IsClinicalInfoValid
    {
        get
        {
            if (PatientInfo is not null)
            {
                if (!PatientInfo.Age.HasValue) return false;
                if (string.IsNullOrEmpty(PatientInfo.Gender)) return false;
            }
            if (Questionnaire is not null)
            {
                if (string.IsNullOrEmpty(Questionnaire.Resource)) return false;
            }
            else
            {
                if (string.IsNullOrEmpty(Text)) return false;
            }
            return true;
        }

    }

}

public class PatientInfo
{
    public int? Age { get; set; }   
    public string? Gender { get; set; }

}