namespace iPath.EF.Core.FeatureHandlers.Questionnaires;

public class CreateQuestionnaireInputHandler(iPathDbContext db, IUserSession sess)
    : IRequestHandler<UpdateQuestionnaireCommand, Task<Guid>>
{
    public async Task<Guid> Handle(UpdateQuestionnaireCommand request, CancellationToken ct)
    {
        Guard.Against.NullOrEmpty(request.QuestionnaireId);
        Guard.Against.NullOrEmpty(request.Resource);

        // for new questionnaires, check that Id is not taken
        if (request.insert)
        {
            if (await db.Questionnaires.AnyAsync(q => q.QuestionnaireId == request.QuestionnaireId, ct))
            {
                throw new InvalidOperationException($"Questionnaire with Id {request.QuestionnaireId} exists already");
            }
        }


        // TODO: Resource should be validated as FHIR Questionnaire

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
                IsActive = true
            };

            await db.Questionnaires.AddAsync(newItem);
            await db.SaveChangesAsync(ct);
            await tran.CommitAsync(ct);

            return newItem.Id;

            }
        catch (Exception ex)
        {
            await tran.RollbackAsync(ct);
            throw ex;
        }
    }
}
