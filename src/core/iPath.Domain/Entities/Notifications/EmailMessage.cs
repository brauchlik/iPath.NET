namespace iPath.Domain.Entities;

public class EmailMessage : BaseEntity
{
    [EmailAddress]
    public required string Receiver { get; set; }

    public required string Subject { get; set; }

    public required string Body { get; set; }

    public DateTime CreatedOn { get; set; }

    public DateTime? SentOn { get; set; }
    public string? ErrorMessage { get; set; }

}
