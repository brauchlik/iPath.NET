using iPath.Application.Services;

namespace iPath.EF.Core.FeatureHandlers.Questionnaires;

public class DeleteQuestionnaireHandler(iPathDbContext db, QuestionnaireCacheServer cache)
    : IRequestHandler<DeleteQuestionnaireCommand, Task<Guid>>
{
    public async Task<Guid> Handle(DeleteQuestionnaireCommand request, CancellationToken ct)
    {
        var q = await db.Questionnaires
            .Include(x => x.Groups)
            .Include(x => x.Communities)
            .FirstOrDefaultAsync(x => x.Id == request.Id, ct);
        Guard.Against.NotFound(request.Id, q);

        // Groups/Communities reference this specific version by its row Id (FK, DeleteBehavior.NoAction) -
        // deleting it out from under an active assignment would break the case-creation wizard's lookup.
        if (q.Groups.Count > 0 || q.Communities.Count > 0)
        {
            throw new ArgumentException(
                $"Cannot delete '{q.Name}': still assigned to {q.Groups.Count} group(s) and {q.Communities.Count} " +
                $"community/communities. Remove those assignments first, or deactivate it instead.");
        }

        db.Questionnaires.Remove(q);
        await db.SaveChangesAsync(ct);

        cache.ClearCache(q.QuestionnaireId);

        return q.Id;
    }
}
