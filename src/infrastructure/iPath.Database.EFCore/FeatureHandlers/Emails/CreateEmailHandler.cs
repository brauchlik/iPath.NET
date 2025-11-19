namespace iPath.EF.Core.FeatureHandlers.Emails;

public class CreateEmailHandler(iPathDbContext db, IEmailQueue queue)
     : IRequestHandler<CreateEmailCommand, Task<EmailMessage>>
{
    public async Task<EmailMessage> Handle(CreateEmailCommand request, CancellationToken cancellationToken)
    {
        var mail = new EmailMessage
        {
            Id = Guid.CreateVersion7(),
            CreatedOn = DateTime.UtcNow,
            Receiver = request.address,
            Subject = request.subject,
            Body = request.body
        };
        await db.EmailStore.AddAsync(mail, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await queue.EnqueueAsync(mail);
        return mail;
    }
}