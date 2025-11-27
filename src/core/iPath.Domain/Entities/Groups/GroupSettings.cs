using Microsoft.EntityFrameworkCore;

namespace iPath.Domain.Entities;

public class GroupSettings
{
    public string Purpose { get; set; } = "";

    public bool DescriptionAllowHtml { get; set; } = true;
    public string DescriptionTemplate { get; set; } = "";

    public bool AnnotationsHide { get; set; } = false;

    public GroupSettings Clone() => (GroupSettings)MemberwiseClone();
}
