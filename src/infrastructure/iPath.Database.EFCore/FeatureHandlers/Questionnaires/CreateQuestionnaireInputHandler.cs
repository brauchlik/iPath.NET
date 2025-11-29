namespace iPath.EF.Core.FeatureHandlers.Questionnaires;

public class CreateQuestionnaireInputHandler(iPathDbContext db, IUserSession sess)
    : IRequestHandler<CreateQuestionnaireCommand, Task<Guid>>
{
    public async Task<Guid> Handle(CreateQuestionnaireCommand request, CancellationToken ct)
    {
        Guard.Against.NullOrEmpty(request.QuestionnaireId);
        Guard.Against.NullOrEmpty(request.Resource);

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
            var newItem = new Questionnaire
            {
                Id = Guid.CreateVersion7(),
                OwnerId = sess.User.Id,
                CreatedOn = DateTime.UtcNow,
                QuestionnaireId = request.QuestionnaireId,
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
