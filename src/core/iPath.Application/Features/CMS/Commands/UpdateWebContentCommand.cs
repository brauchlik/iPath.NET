namespace iPath.Application.Features.CMS;

public record UpdateWebContentCommand(Guid Id, string Title, string Body)
    : IRequest<CreateWebContentCommand, Task<WebContentDto>>;
