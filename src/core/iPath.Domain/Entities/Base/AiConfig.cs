namespace iPath.Domain.Entities;

public class AiConfig
{
    public bool IsEnabled { get; set; } = false;
    public string? SystemInstructionsOverride { get; set; }
    public string? PreferredModelId { get; set; }

    public AiConfig Clone() => (AiConfig)MemberwiseClone();
}
