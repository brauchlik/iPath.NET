using iPath.Application.AI;

namespace iPath.Database.EFCore.AI;

public class PromptContextResolver : IPromptContextResolver
{
    private readonly iPathDbContext _dbContext;

    public PromptContextResolver(iPathDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<AiConfig> ResolveConfigAsync(Guid? communityId, Guid? groupId, Guid? userId)
    {
        var config = new AiConfig
        {
            IsEnabled = false,
            SystemInstructionsOverride = GetDefaultSystemPrompt()
        };

        if (communityId.HasValue)
        {
            var community = await _dbContext.Communities
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == communityId.Value);

            if (community?.Settings?.AiSettings != null)
            {
                var commAi = community.Settings.AiSettings;
                config.IsEnabled = commAi.IsEnabled;
                if (!string.IsNullOrWhiteSpace(commAi.SystemInstructionsOverride))
                {
                    config.SystemInstructionsOverride = commAi.SystemInstructionsOverride;
                }
                if (!string.IsNullOrWhiteSpace(commAi.PreferredModelId))
                {
                    config.PreferredModelId = commAi.PreferredModelId;
                }
            }
        }

        if (groupId.HasValue)
        {
            var group = await _dbContext.Groups
                .AsNoTracking()
                .FirstOrDefaultAsync(g => g.Id == groupId.Value);

            if (group?.Settings?.AiSettings != null)
            {
                var groupAi = group.Settings.AiSettings;
                config.IsEnabled = groupAi.IsEnabled;
                if (!string.IsNullOrWhiteSpace(groupAi.SystemInstructionsOverride))
                {
                    config.SystemInstructionsOverride = groupAi.SystemInstructionsOverride;
                }
                if (!string.IsNullOrWhiteSpace(groupAi.PreferredModelId))
                {
                    config.PreferredModelId = groupAi.PreferredModelId;
                }
            }
        }

        if (userId.HasValue)
        {
            var user = await _dbContext.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId.Value);

            if (user?.Profile?.AiSettings != null)
            {
                var userAi = user.Profile.AiSettings;
                config.IsEnabled = userAi.IsEnabled;
                if (!string.IsNullOrWhiteSpace(userAi.SystemInstructionsOverride))
                {
                    config.SystemInstructionsOverride = userAi.SystemInstructionsOverride;
                }
                if (!string.IsNullOrWhiteSpace(userAi.PreferredModelId))
                {
                    config.PreferredModelId = userAi.PreferredModelId;
                }
            }
        }

        return config;
    }

    public string GetDefaultSystemPrompt()
    {
        return """
            You are an advanced clinical entity extraction assistant specialized in pathology case intake.
            Analyze the provided unstructured medical or clinical note and extract the following information in strict JSON format:
            1. Age (integer, or null if not mentioned).
            2. Sex (string: "M" for male, "F" for female, "U" for unknown).
            3. TopographyCode (ICD-O-3 topography code, e.g., "C50.9", or null if not mentioned).
            4. TopographyName (the descriptive name of the topography site, e.g., "Breast", or null if not mentioned).
            5. ClinicalQuestions (a JSON array of strings containing specific diagnostic/clinical questions or queries).
            6. Snippet (the exact text snippet from the input note supporting the topography suggestion, or null).

            Your response must be ONLY the raw JSON object, without markdown formatting blocks or any extra text.

            JSON Schema:
            {
              "Age": 45,
              "Sex": "F",
              "TopographyCode": "C50.9",
              "TopographyName": "Breast",
              "ClinicalQuestions": [
                "Is it invasive ductal carcinoma?"
              ],
              "Snippet": "lump in the breast"
            }
            """;
    }
}
