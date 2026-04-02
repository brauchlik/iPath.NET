using iPath.Application.Features.EmailImport;

namespace iPath.Application.Contracts;

public interface IEmailImportService
{
    Task<IReadOnlyList<ImportMailboxSummary>> GetMailboxesAsync(CancellationToken ct);
    Task<IReadOnlyList<ImportEmailPreview>> GetPendingAsync(string mailboxName, CancellationToken ct);
    Task<ImportEmailPreview?> GetPreviewAsync(string mailboxName, string messageId, CancellationToken ct);
    Task<ImportEmailResult> ImportSingleAsync(string mailboxName, string messageId, bool forceReimport = false, CancellationToken ct = default);
    Task<IReadOnlyList<ImportEmailResult>> ImportAllPendingAsync(CancellationToken ct);
    Task DeleteAsync(string mailboxName, string messageId, CancellationToken ct);
}