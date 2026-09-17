using Hl7.Fhir.Introspection;
using Hl7.Fhir.Serialization;
using iPath.Application.Features.Questionnaires;
using iPath.Application.Services;
using System.Text.Json;
using FhirQuestionnaireResponse = Hl7.Fhir.Model.QuestionnaireResponse;

namespace iPath.EF.Core.FeatureHandlers.Questionnaires.Services;

/// <summary>
/// Resolves a saved QuestionnaireResponse into ServiceRequestAnswer rows. Derives data only:
/// rows can be dropped and rebuilt at any time, the response resource stays the source of truth.
/// Deliberately does not call SaveChanges - the caller saves, so rows and the response state are
/// written in the same unit of work as the description that produced them.
/// </summary>
public class ServiceRequestAnswerExtractionService(iPathDbContext db,
    IMediator mediator,
    QuestionnaireCacheServer cache,
    IQuestionnaireAnswerExtractor extractor,
    ILogger<ServiceRequestAnswerExtractionService> logger)
{
    /// <summary>Pins the answered definition version on first use (legacy responses have none).</summary>
    public async Task<int?> ResolveVersionAsync(QuestionnaireResponseData qr, CancellationToken ct = default)
    {
        if (qr.Version.HasValue || string.IsNullOrEmpty(qr.QuestionnaireId)) return qr.Version;

        var entity = await mediator.Send(new GetQuestionnaireQuery(qr.QuestionnaireId), ct);
        if (entity is not null)
        {
            qr.Version = entity.Version;
        }
        return qr.Version;
    }

    public async Task<int> ExtractAsync(ServiceRequest node, FhirQuestionnaireResponse? parsed = null, CancellationToken ct = default)
    {
        var qr = node.Description?.Questionnaire;

        try
        {
            if (qr is null || string.IsNullOrEmpty(qr.Resource))
            {
                await RemoveRowsAsync(node.Id, ct);
                qr?.ResetExtractionState();
                return 0;
            }

            await ResolveVersionAsync(qr, ct);

            var questionnaire = await cache.GetQuestionnaireAsync(qr.QuestionnaireId, qr.Version);
            if (questionnaire is null)
            {
                throw new InvalidOperationException($"Questionnaire {qr.QuestionnaireId} version {qr.Version} is not available");
            }

            var settings = await cache.GetSettingsAsync(qr.QuestionnaireId, qr.Version);
            if (settings is not null && !settings.ShouldExtractAnswers())
            {
                // switched off for this questionnaire: the form contributes no answers at all
                await RemoveRowsAsync(node.Id, ct);
                qr.SetExtractionState(extractor.Version, 0, disabled: true);
                logger.LogInformation("Answer extraction skipped for case {ServiceRequestId}: switched off for questionnaire {QuestionnaireId}",
                    node.Id, qr.QuestionnaireId);
                return 0;
            }

            var response = parsed;
            if (response is null)
            {
                var options = new JsonSerializerOptions().ForFhir(Hl7.Fhir.Model.ModelInfo.ModelInspector);
                response = JsonSerializer.Deserialize<FhirQuestionnaireResponse>(qr.Resource, options)
                    ?? throw new InvalidOperationException("QuestionnaireResponse could not be deserialized");
            }

            var answers = extractor.Extract(response, questionnaire);

            await RemoveRowsAsync(node.Id, ct);
            foreach (var answer in answers)
            {
                db.ServiceRequestAnswers.Add(new ServiceRequestAnswer
                {
                    ServiceRequestId = node.Id,
                    QuestionnaireId = qr.QuestionnaireId,
                    QuestionnaireVersion = qr.Version,
                    LinkId = Truncate(answer.LinkId, 500)!,
                    CodeSystem = Truncate(answer.CodeSystem, 200),
                    Code = Truncate(answer.Code, 100),
                    CodeDisplay = Truncate(answer.CodeDisplay, 500),
                    OtherCodings = Truncate(answer.OtherCodings, 2000),
                    ValueType = Truncate(answer.ValueType, 32) ?? "string",
                    Value = Truncate(answer.Value, 2000),
                    ValueDisplay = Truncate(answer.ValueDisplay, 2000),
                    Unit = Truncate(answer.Unit, 100),
                    ExtractionVersion = extractor.Version,
                    CreatedOn = DateTime.UtcNow
                });
            }

            qr.SetExtractionState(extractor.Version, answers.Count);
            logger.LogInformation("Answer extraction for case {ServiceRequestId}: {Rows} rows from {Items} top level items (questionnaire {QuestionnaireId} version {Version})",
                node.Id, answers.Count, response.Item?.Count ?? 0, qr.QuestionnaireId, qr.Version);
            return answers.Count;
        }
        catch (Exception ex)
        {
            // a failed extraction must never break saving the case; rows from the last successful
            // run are left in place and the error is recorded for the review views
            logger.LogError(ex, "Answer extraction failed for service request {ServiceRequestId}", node.Id);
            if (qr is not null) qr.ExtractionError = Truncate(ex.Message, 1000);
            return 0;
        }
    }

    private async Task RemoveRowsAsync(Guid serviceRequestId, CancellationToken ct)
    {
        var existing = await db.ServiceRequestAnswers
            .Where(a => a.ServiceRequestId == serviceRequestId)
            .ToListAsync(ct);

        if (existing.Count > 0) db.ServiceRequestAnswers.RemoveRange(existing);
    }

    private static string? Truncate(string? value, int maxLength)
        => value is null || value.Length <= maxLength ? value : value[..maxLength];
}

public static class QuestionnaireResponseDataExtensions
{
    public static void ResetExtractionState(this QuestionnaireResponseData qr)
    {
        qr.ExtractedOn = null;
        qr.ExtractionVersion = null;
        qr.ExtractedAnswerCount = null;
        qr.ExtractionError = null;
        qr.ExtractionDisabled = false;
    }

    public static void SetExtractionState(this QuestionnaireResponseData qr, int extractionVersion, int answerCount, bool disabled = false)
    {
        qr.ExtractedOn = DateTime.UtcNow;
        qr.ExtractionVersion = extractionVersion;
        qr.ExtractedAnswerCount = answerCount;
        qr.ExtractionError = null;
        qr.ExtractionDisabled = disabled;
    }
}
