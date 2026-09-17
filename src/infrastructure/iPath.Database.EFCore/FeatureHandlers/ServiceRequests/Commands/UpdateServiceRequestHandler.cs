using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using iPath.Application.Features.Questionnaires;
using iPath.Application.Services;
using Microsoft.Extensions.DependencyInjection;
using iPath.EF.Core.FeatureHandlers.Users;
using iPath.EF.Core.FeatureHandlers.Questionnaires.Services;
using System.Text.Json;

namespace iPath.EF.Core.FeatureHandlers.ServiceRequests.Commands;


public class UpdateServiceRequestHandler(iPathDbContext db, IMediator mediator,
    IServiceProvider sp,
    QuestionnaireCacheServer cache,
    IQuestionnaireToTextServiceRegistry previewRegistry,
    ServiceRequestAnswerExtractionService answerExtraction,
    ILogger<UpdateServiceRequestHandler> logger,
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

        // Questionnaire to Text and answer extraction
        var qr = request.Description?.Questionnaire;
        if (qr is not null && !string.IsNullOrEmpty(qr.Resource))
        {
            QuestionnaireResponse? response = null;
            Questionnaire? definition = null;
            var loadFailed = false;

            try
            {
                // pin the answered definition version before anything resolves against it
                await answerExtraction.ResolveVersionAsync(qr, ct);

                var options = new JsonSerializerOptions().ForFhir(ModelInfo.ModelInspector);
                response = JsonSerializer.Deserialize<QuestionnaireResponse>(qr.Resource, options);
                definition = await cache.GetQuestionnaireAsync(qr.QuestionnaireId, qr.Version);
            }
            catch (Exception ex)
            {
                loadFailed = true;
                logger.LogError(ex, "Reading questionnaire response failed for {ServiceRequestId}", request.ServiceRequestId);
            }

            if (definition is not null && response is not null)
            {
                try
                {
                    var settings = await cache.GetSettingsAsync(qr.QuestionnaireId, qr.Version);
                    var serviceKey = settings?.TextPreviewService;
                    var fallbackKey = previewRegistry.GetDefault().Key;
                    var q2t = (string.IsNullOrEmpty(serviceKey) ? null : sp.GetKeyedService<IQuestionnaireToTextService>(serviceKey))
                              ?? sp.GetRequiredKeyedService<IQuestionnaireToTextService>(fallbackKey);

                    request.Description.Questionnaire.GeneratedText = q2t.CreateText(response, definition);
                }
                catch (Exception)
                {
                    qr.GeneratedText = "";
                }
            }
            else if (loadFailed)
            {
                qr.GeneratedText = "";
            }

            await answerExtraction.ExtractAsync(node, response, ct);
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