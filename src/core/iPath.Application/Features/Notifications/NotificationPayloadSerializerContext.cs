using System.Text.Json.Serialization;

namespace iPath.Application.Features.Notifications;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(NotificationPayload))]
public partial class NotificationPayloadSerializerContext : JsonSerializerContext
{
}
