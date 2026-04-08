namespace iPath.Domain.Entities;

public class EmailImportSettings
{
    public Guid? DefaultGroupId { get; set; }

    public EmailImportSettings Clone()
    {
        var clone = (EmailImportSettings)this.MemberwiseClone();
        return clone;
    }
}