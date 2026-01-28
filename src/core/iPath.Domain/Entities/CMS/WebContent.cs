namespace iPath.Domain.Entities;

public class WebContent : AuditableEntity
{
    public string Title { get; set; }
    public string Body { get; set; }

    public Guid OwnerId { get; set; }
    public User Owner { get; set; }

    public eWebContentType Type { get; set; }

    public Guid? ParentId { get; set; }
    public WebContent? Parent { get; set; }
    public ICollection<WebContent> ChildContent { get; set; } = [];
}


public enum eWebContentType
{
    none = 0,
    page = 1,
    news = 2,
    help = 3
}