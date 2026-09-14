using Hl7.Fhir.Utility;
using iPath.Application.Services;

namespace iPath.EF.Core.FeatureHandlers.Questionnaires;

public class CreateQuestionnaireInputHandler(iPathDbContext db, QuestionnaireCacheServer cache, IUserSession sess)
    : IRequestHandler<UpdateQuestionnaireCommand, Task<Guid>>
{
    public async Task<Guid> Handle(UpdateQuestionnaireCommand request, CancellationToken ct)
    {
        Guard.Against.NullOrEmpty(request.QuestionnaireId);
        Guard.Against.NullOrEmpty(request.Resource);

        if (request.insert)
        {
            // insert=true: this is a brand-new questionnaire. Reject if the Id is already taken.
            if (await db.Questionnaires.AnyAsync(q => q.QuestionnaireId == request.QuestionnaireId, ct))
            {
                throw new InvalidOperationException($"Questionnaire with Id {request.QuestionnaireId} exists already");
            }
        }
        else
        {
            // insert=false: this is a new version of an existing questionnaire. Reject if the Id
            // is unknown - updating/upgrading must only ever be called with a known QuestionnaireId.
            if (!await db.Questionnaires.AnyAsync(q => q.QuestionnaireId == request.QuestionnaireId, ct))
            {
                throw new InvalidOperationException($"Questionnaire with Id {request.QuestionnaireId} does not exist");
            }
        }


        // TODO: Resource should be validated as FHIR Questionnaire
        // TODO: Create new Version only if Resource has changed

        await using var tran = await db.Database.BeginTransactionAsync(ct);

        try
        {
            // set existing questionnaires to inactive
            await db.Questionnaires
                .Where(q => q.QuestionnaireId == request.QuestionnaireId)
                .ExecuteUpdateAsync(setter => setter.SetProperty(q => q.IsActive, false), ct);


            // get next VersionNr (Max + 1)
            var maxVersion = await db.Questionnaires
                .Where(q => q.QuestionnaireId == request.QuestionnaireId)
                .MaxAsync(q => (int?)q.Version, ct) ?? 0;

            // create new entry in DB
            var newItem = new QuestionnaireEntity
            {
                Id = Guid.CreateVersion7(),
                OwnerId = sess.User.Id,
                CreatedOn = DateTime.UtcNow,
                QuestionnaireId = request.QuestionnaireId,
                Name = request.Name,
                Version = maxVersion + 1,
                Resource = request.Resource,
                IsActive = request.IsActive,
                Settings = request.Settings ?? new()
            };

            await db.Questionnaires.AddAsync(newItem);
            await db.SaveChangesAsync(ct);
            await tran.CommitAsync(ct);

            // clear from cache
            cache.ClearCache(request.QuestionnaireId);

            return newItem.Id;

        }
        catch (Exception ex)
        {
            await tran.RollbackAsync(ct);
            throw ex;
        }
    }
}
