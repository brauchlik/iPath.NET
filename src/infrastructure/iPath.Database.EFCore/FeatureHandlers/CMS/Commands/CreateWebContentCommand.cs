using iPath.Application.Features.CMS;

namespace iPath.EF.Core.FeatureHandlers;

public class CreateWebContentCommandHandler(iPathDbContext db, IUserSession sess)
    : IRequestHandler<CreateWebContentCommand, Task<WebContentDto>>
{
    public async Task<WebContentDto> Handle(CreateWebContentCommand request, CancellationToken cancellationToken)
    {
        if (!sess.IsAdmin)
        {
            throw new NotAllowedException();
        }
        var content = new WebContent
        {
            Id = Guid.CreateVersion7(),
            Title = request.Title,
            Body = request.Body,
            Type = request.Type,
            OwnerId = sess.User.Id
        };

        await db.WebPages.AddAsync(content, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        return content.ToDto();
    }
}
