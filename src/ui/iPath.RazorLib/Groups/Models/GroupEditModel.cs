using iPath.Domain.Entities;
using System.ComponentModel.DataAnnotations;

namespace iPath.Blazor.Componenents.Groups;

public class GroupEditModel
{
    public Guid? Id { get; set; }
    [Required]
    public string Name { get; set; } = "";
    public GroupSettings Settings { get; set; } = new();
    public OwnerDto? Owner { get; set; }
    public CommunityListDto? Community { get; set; }
    public string? CommunityTopographyValueSet { get; set; }
    public string? CommunityMorphologyValueSet { get; set; }
}
