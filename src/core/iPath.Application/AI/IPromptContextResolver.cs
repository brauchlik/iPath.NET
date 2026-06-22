using iPath.Domain.Entities;

namespace iPath.Application.AI;

public interface IPromptContextResolver
{
    Task<AiConfig> ResolveConfigAsync(Guid? communityId, Guid? groupId, Guid? userId);
    string GetDefaultSystemPrompt();
}
