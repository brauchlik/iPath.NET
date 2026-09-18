namespace VsiConverter.UI.Models;

public enum ConversionStatus
{
    Queued,
    CheckingCompanion,
    DetectingSeries,
    Converting,
    Zipping,
    Completed,
    Failed,
    Cancelled
}
