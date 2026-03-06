using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using iPath.Application.Features.Questionnaires;
using iPath.Blazor.ServiceLib.Services;
using iPath.EF.Core.FeatureHandlers.Users;
using System.Text.Json;

namespace iPath.EF.Core.FeatureHandlers.ServiceRequests.Commands;


public class UpdateServiceRequestHandler(iPathDbContext db, IMediator mediator, 
    IQuestionnaireToTextService q2t,
    QuestionnaireCacheServer cache,
    IUserSession sess)
    : IRequestHandler<UpdateServiceRequestCommand, Task<bool>>
{
    public async Task<bool> Handle(UpdateServiceRequestCommand request, CancellationToken ct)
    {
        var node = await db.ServiceRequests
            .SingleOrDefaultAsync(n => n.Id == request.ServiceRequestId, ct);
        Guard.Against.NotFound(request.ServiceRequestId, node);

        // permission
        if (!sess.IsAdmin)
        {
            if (!sess.IsGroupModerator(node.GroupId))
            {
                if (node.OwnerId != sess.User.Id)
                {
                    throw new NotAllowedException();
                }
            }
        }

        node.UpdateNode(request, sess.User.Id);

        // Questionnaire to Text
        var qr = request.Description?.Questionnaire;
        if (qr is not null && !string.IsNullOrEmpty(qr.Resource))
        {
            try
            {
                var options = new JsonSerializerOptions().ForFhir(ModelInfo.ModelInspector);
                var r = JsonSerializer.Deserialize<QuestionnaireResponse>(qr.Resource, options);

                var q = await cache.GetQuestionnaireAsync(qr.QuestionnaireId);
                if (q is not null)
                {
                    request.Description.Questionnaire.GeneratedText = q2t.CreateText(r, q);
                }
            }
            catch (Exception ex) 
            {
                qr.GeneratedText = "";
            }

        }


        if (request.NewOwnerId.HasValue)
        {
            // Specification for UserId and Group Membership (not banned)
            Specification<User> spec = new UserHasIdSpecifications(request.NewOwnerId.Value);
            spec = spec.And(new UserIsGroupMemberSpecifications(node.GroupId));

            var newOwner = await db.Users
                .AsNoTracking()
                .Where(spec.ToExpression())
                .SingleOrDefaultAsync(ct);

            Guard.Against.NotFound(request.NewOwnerId.Value, newOwner);
            node.OwnerId = newOwner.Id;
        }

        if (request.NewGroupId.HasValue)
        {
            // Specification for UserId and Group Membership (Owner must be in new group and not banned)
            Specification<User> spec = new UserHasIdSpecifications(node.OwnerId);
            spec = spec.And(new UserIsGroupMemberSpecifications(request.NewGroupId.Value));

            var newOwner = await db.Users
                .AsNoTracking()
                .Where(spec.ToExpression())
                .SingleOrDefaultAsync(ct);

            Guard.Against.Null(newOwner, "NewGroupId", "Request owner is not member of the new group");
            node.GroupId = request.NewGroupId.Value;
        }

        await db.SaveChangesAsync(ct);

        // update user visit
        await mediator.Send(new UpdateServiceRequestVisitCommand(node.Id), ct);

        return true;
    }
}