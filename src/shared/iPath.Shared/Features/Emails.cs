using System.ComponentModel.DataAnnotations;

namespace iPath.Shared.Features;


internal class Emails { }

public record SendMailInput([EmailAddress] string email, 
    [Required] string subject, 
    [Required] string body, 
    DateTime createdOn);
