using Microsoft.EntityFrameworkCore;

namespace iPath.Domain.Entities;

public class GroupSettings
{
    public string Purpose { get; set; } = "";

    public bool DescriptionAllowHtml { get; set; } = true;
    public string DescriptionTemplate { get; set; } = "";

    public bool AnnotationsHide { get; set; } = false;

    public ICollection<string> CaseTypes { get; set; } = [];

    public GroupSettings Clone() => (GroupSettings)MemberwiseClone();
}
