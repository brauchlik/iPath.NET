
using Microsoft.Extensions.Logging;

namespace iPath.EF.Core.FeatureHandlers.Questionnaires;

public class AssignQuestionnaireCommandHandler(iPathDbContext db, IUserSession sess, ILogger<AssignQuestionnaireCommandHandler> logger)
    : IRequestHandler<AssignQuestionnaireCommand, Task>
{
    public async Task Handle(AssignQuestionnaireCommand cmd, CancellationToken ct)
    {
        var questionnaire = await db.Questionnaires.FindAsync(cmd.Id, ct);
        Guard.Against.NotFound(cmd.Id, questionnaire);

        if (cmd.GroupId.HasValue)
        {
            var group = await db.Groups.FindAsync(cmd.GroupId.Value, ct);
            Guard.Against.NotFound(cmd.GroupId.Value, group);

            var item = await db.Set<QuestionnaireForGroup>()
                .FirstOrDefaultAsync(q => q.QuestionnaireId == cmd.Id && q.GroupId == cmd.GroupId.Value && q.Usage == cmd.Usage);

            if (cmd.remove && item is not null)
            {
                db.Set<QuestionnaireForGroup>().Remove(item);
            }
            else if (!cmd.remove && item is null)
            {
                item = new QuestionnaireForGroup
                {
                    GroupId = cmd.GroupId.Value,
                    QuestionnaireId = cmd.Id,
                    Usage = cmd.Usage
                };
                await db.Set<QuestionnaireForGroup>().AddAsync(item, ct);
            }
            if (item != null)
            {
                item.Priority = cmd.Priority;
            }
        }
        else if (cmd.CommunityId.HasValue)
        {
            var community = await db.Communities.FindAsync(cmd.CommunityId.Value, ct);
            Guard.Against.NotFound(cmd.CommunityId.Value, community);

            var item = await db.Set<QuestionnaireForCommunity>()
                .FirstOrDefaultAsync(q => q.QuestionnaireId == cmd.Id && q.CommunityId == cmd.CommunityId.Value && q.Usage == cmd.Usage);

            if (cmd.remove && item is not null)
            {
                db.Set<QuestionnaireForCommunity>().Remove(item);
            }
            else if (!cmd.remove && item is null)
            {
                item = new QuestionnaireForCommunity
                {
                    CommunityId = cmd.CommunityId.Value,
                    QuestionnaireId = cmd.Id,
                    Usage = cmd.Usage
                };
                await db.Set<QuestionnaireForCommunity>().AddAsync(item, ct);
            }
        }

        await db.SaveChangesAsync(ct);
    }
}
