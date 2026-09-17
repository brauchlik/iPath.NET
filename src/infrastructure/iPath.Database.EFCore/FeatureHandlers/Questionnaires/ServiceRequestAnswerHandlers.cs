using iPath.Application.Features.Questionnaires;
using iPath.Application.Features.Questionnaires.Commands;
using iPath.EF.Core.FeatureHandlers.Questionnaires.Services;

namespace iPath.EF.Core.FeatureHandlers.Questionnaires;

public class ReExtractServiceRequestAnswersHandler(iPathDbContext db, ServiceRequestAnswerExtractionService extraction)
    : IRequestHandler<ReExtractServiceRequestAnswersCommand, Task<int>>
{
    public async Task<int> Handle(ReExtractServiceRequestAnswersCommand request, CancellationToken ct)
    {
        var node = await db.ServiceRequests.SingleOrDefaultAsync(x => x.Id == request.ServiceRequestId, ct);
        Guard.Against.NotFound(request.ServiceRequestId, node);

        var count = await extraction.ExtractAsync(node, null, ct);
        await db.SaveChangesAsync(ct);
        return count;
    }
}

public class BackfillServiceRequestAnswersHandler(iPathDbContext db,
    ServiceRequestAnswerExtractionService extraction,
    IQuestionnaireAnswerExtractor extractor,
    ILogger<BackfillServiceRequestAnswersHandler> logger)
    : IRequestHandler<BackfillServiceRequestAnswersCommand, Task<BackfillAnswersResult>>
{
    public async Task<BackfillAnswersResult> Handle(BackfillServiceRequestAnswersCommand request, CancellationToken ct)
    {
        var batchSize = request.BatchSize <= 0 ? 100 : request.BatchSize;
        var scanned = 0;
        var extracted = 0;
        var answers = 0;
        var offset = 0;

        while (!ct.IsCancellationRequested)
        {
            var batch = await db.ServiceRequests
                .AsNoTracking()
                .OrderBy(x => x.CreatedOn)
                .Skip(offset)
                .Take(batchSize)
                .Select(x => new { x.Id, x.Description })
                .ToListAsync(ct);

            if (batch.Count == 0) break;
            offset += batch.Count;
            scanned += batch.Count;

            foreach (var candidate in batch)
            {
                var qr = candidate.Description?.Questionnaire;
                if (qr is null || string.IsNullOrEmpty(qr.Resource)) continue;

                // already extracted with the current rules and no recorded error
                if (qr.ExtractionVersion == extractor.Version && string.IsNullOrEmpty(qr.ExtractionError)) continue;

                var node = await db.ServiceRequests.SingleAsync(x => x.Id == candidate.Id, ct);
                answers += await extraction.ExtractAsync(node, null, ct);
                await db.SaveChangesAsync(ct);
                db.ChangeTracker.Clear();
                extracted++;

                if (request.MaxCases > 0 && extracted >= request.MaxCases)
                {
                    logger.LogInformation("Answer backfill stopped at the requested limit of {MaxCases}", request.MaxCases);
                    return new BackfillAnswersResult(scanned, extracted, answers);
                }
            }
        }

        logger.LogInformation("Answer backfill scanned {Scanned}, extracted {Extracted}, wrote {Answers} answers",
            scanned, extracted, answers);

        return new BackfillAnswersResult(scanned, extracted, answers);
    }
}
