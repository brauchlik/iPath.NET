namespace iPath.Application.Features.Documents;

public record DocumentDto
{
    public string DocumentType { get; init; } = default!;
    public int? SortNr { get; set; }

    public Guid Id { get; init; }
    public DateTime CreatedOn { get; set; }

    public Guid OwnerId { get; init; }
    public required OwnerDto Owner { get; init; }

    public Guid ServiceRequestId { get; init; }
    public Guid? ParentNodeId { get; init; }

    public NodeFile? File { get; init; } = new()!;

    public int? ipath2_id { get; init; }
}
