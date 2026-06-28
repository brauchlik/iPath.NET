namespace iPath.Domain.Entities;

public class CommunitySettings
{
    public string? Description { get; set; }

    public bool DescriptionAllowHtml { get; set; } = true;
    public string DescriptionTemplate { get; set; } = "";
    
    public string? BaseUrl { get; set; }

    private ICollection<string> _caseTypes = [];
    public ICollection<string> CaseTypes
    {
        get => _caseTypes ??= [];
        set => _caseTypes = value ?? [];
    }


    public string? MorphologyValueSet { get; set; }
    public string? TopographyValueSet { get; set; }


    public StorageInfo? Storage { get; set; }

    public AiConfig AiSettings { get; set; } = new();

    public CommunitySettings Clone() => (CommunitySettings)MemberwiseClone();
}
