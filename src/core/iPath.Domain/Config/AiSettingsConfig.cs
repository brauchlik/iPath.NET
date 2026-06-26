namespace iPath.Domain.Config;

public class AiSettingsConfig
{
    public const string ConfigName = "AiSettings";

    public bool IsEnabled { get; set; }
    public bool BackfillEnabled { get; set; }
    public string Provider { get; set; } = "Ollama";
}
