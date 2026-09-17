namespace iPath.Application.Features.Questionnaires.Commands;

public record ReExtractServiceRequestAnswersCommand(Guid ServiceRequestId)
    : IRequest<ReExtractServiceRequestAnswersCommand, Task<int>>;

public record BackfillServiceRequestAnswersCommand(int BatchSize = 100, int MaxCases = 0)
    : IRequest<BackfillServiceRequestAnswersCommand, Task<BackfillAnswersResult>>;

public record BackfillAnswersResult(int CasesScanned, int CasesExtracted, int AnswersWritten);
