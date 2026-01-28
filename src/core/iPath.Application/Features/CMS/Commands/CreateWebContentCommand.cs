namespace iPath.Application.Features.CMS;

public record CreateWebContentCommand(string Title, string Body, eWebContentType Type)
    : IRequest<CreateWebContentCommand, Task<WebContentDto>>;
