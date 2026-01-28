namespace iPath.Application.Features.CMS;

public record WebContentDto(Guid Id, string Title, string Body, eWebContentType Type, DateTime CreatedOn, OwnerDto Owner);