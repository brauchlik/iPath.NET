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

    public string? ProvisionalDiagnosis { get; set; } = string.Empty!;
    public CodedConcept? ProvisionalDiagnosisCode { get; set; }

    public PatientInfo PatientInfo { get; set; } = new();

    public QuestionnaireResponseData? Questionnaire { get; set; }

    public CodedConcept? BodySite { get; set; }

    public RequestDescription Clone() => (RequestDescription)MemberwiseClone();

    public string GetExtractionText()
    {
        var sb = new System.Text.StringBuilder();
        if (!string.IsNullOrWhiteSpace(Title))
        {
            sb.Append("Title: ");
            sb.AppendLine(Title);
        }
        if (!string.IsNullOrWhiteSpace(Subtitle))
        {
            sb.Append("Subtitle: ");
            sb.AppendLine(Subtitle);
        }
        if (!string.IsNullOrWhiteSpace(CaseType))
        {
            sb.Append("CaseType: ");
            sb.AppendLine(CaseType);
        }
        if (!string.IsNullOrWhiteSpace(Text))
        {
            if (sb.Length > 0)
                sb.AppendLine();
            sb.Append(Text);
        }
        return sb.ToString().TrimEnd();
    }


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