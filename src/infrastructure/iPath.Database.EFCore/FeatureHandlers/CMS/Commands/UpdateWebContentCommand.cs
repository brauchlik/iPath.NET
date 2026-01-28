using iPath.Application.Features.CMS;

namespace iPath.EF.Core.FeatureHandlers.CMS.Commands;

public class UpdateWebContentCommandHandler(iPathDbContext db, IUserSession sess)
    : IRequestHandler<UpdateWebContentCommand, Task<WebContentDto>>
{
    public async Task<WebContentDto> Handle(UpdateWebContentCommand request, CancellationToken cancellationToken)
    {
        if (!sess.IsAdmin)
        {
            throw new NotAllowedException();
        }
        var content = await db.WebPages.FirstOrDefaultAsync(x => x.Id == request.Id);
        Guard.Against.NotFound(request.Id, content);

        content.Title = request.Title;
        content.Body = request.Body;
      
        await db.SaveChangesAsync(cancellationToken);

        return content.ToDto();
    }
}