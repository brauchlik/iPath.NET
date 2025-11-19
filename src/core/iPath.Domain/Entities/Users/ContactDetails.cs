namespace iPath.Domain.Entities;

public class ContactDetails
{
    public string? PhoneNr { get; set; }
    public string? Email { get; set; }
    public string? Organisation { get; set; }
    public Address Address { get; set; } = new ();
}



