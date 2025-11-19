namespace iPath.EF.Core.FeatureHandlers.Emails;

internal class SetEmailSentHandler(iPathDbContext db)
    : IRequestHandler<EmailSetSentCommand, Task<EmailMessage>>
{
    public async Task<EmailMessage> Handle(EmailSetSentCommand request, CancellationToken cancellationToken)
    {
        var email = await db.EmailStore.FindAsync(request.id, cancellationToken);
        if (email is not null)
        {
            email.SentOn = DateTime.UtcNow; 
            await db.SaveChangesAsync(cancellationToken);
        }
        return email;
    }
}