namespace iPath.Domain.Entities;


public class ServiceRequestEvent : EventEntity
{
    [JsonIgnore]
    public ServiceRequest ServiceRequest { get; set; }
}


public interface IEventWithNotifications { }