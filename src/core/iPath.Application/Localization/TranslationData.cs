using System.Collections.Concurrent;

namespace iPath.Application.Localization;

public class TranslationData
{
    private ConcurrentDictionary<string, TranslationMetadata> _wordMetadata = new();

    public DateTime? ModifiedOn { get; set; }
    public string locale { get; set; }
    public TranslationDict Words { get; set; } = new();

    public ConcurrentDictionary<string, TranslationMetadata> WordMetadata 
    { 
        get => _wordMetadata ??= new(); 
        set => _wordMetadata = value ?? new(); 
    }
}

public class TranslationDict : ConcurrentDictionary<string, string>;

public class TranslationMetadata
{
    public string ModelUsed { get; set; } = string.Empty;
    public DateTime TranslatedAt { get; set; }
    public bool IsHumanModified { get; set; }
}