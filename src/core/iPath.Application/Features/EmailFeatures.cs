using System.ComponentModel.DataAnnotations;

namespace iPath.Application.Features;


internal class EmailFeatures { }


public class GetEmailsQuery : PagedQuery<EmailMessage>
    , IRequest<GetEmailsQuery, Task<PagedResultList<EmailMessage>>>
{
}

public record CreateEmailCommand([EmailAddress] string address, string subject, string body) 
    : IRequest<CreateEmailCommand, Task<EmailMessage>>;

public record EmailSetSentCommand(Guid id)
    : IRequest<EmailSetSentCommand, Task<EmailMessage>>;

public record EmailSetErrorCommand(Guid id, string error)
    : IRequest<EmailSetSentCommand, Task<EmailMessage>>;