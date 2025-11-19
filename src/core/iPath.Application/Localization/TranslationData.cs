namespace iPath.Application.Localization;

public class TranslationData
{
    public DateTime? ModifiedOn { get; set; }
    public string locale { get; set; }
    public TranslationDict Words { get; set; }
}

public class TranslationDict : Dictionary<string, string>;