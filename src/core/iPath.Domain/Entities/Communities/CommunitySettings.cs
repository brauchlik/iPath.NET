namespace iPath.Domain.Entities;

public class CommunitySettings
{
    public string? Description { get; set; }

    public bool DescriptionAllowHtml { get; set; } = true;
    public string DescriptionTemplate { get; set; } = "";
    
    public string? BaseUrl { get; set; }

    public ICollection<string> CaseTypes { get; set; } = [];


    public string? MorphologyValueSet { get; set; }
    public string? TopographyValueSet { get; set; }


    public StorageInfo? Storage { get; set; }

    public CommunitySettings Clone() => (CommunitySettings)MemberwiseClone();
}
