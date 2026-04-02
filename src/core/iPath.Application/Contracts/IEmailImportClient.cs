using iPath.Application.Features.EmailImport;

namespace iPath.Application.Contracts;

public interface IEmailImportClient : IDisposable
{
    string MailboxName { get; }
    Task ConnectAsync(CancellationToken ct);
    Task<IReadOnlyList<ImportEmailMessage>> GetPendingAsync(CancellationToken ct);
    Task<ImportEmailMessage?> GetAsync(string messageId, CancellationToken ct);
    Task MoveToQuarantineFolderAsync(string messageId, CancellationToken ct);
    Task DeleteAsync(string messageId, CancellationToken ct);
}

public interface IEmailImportClientFactory
{
    IEmailImportClient Create(ImportMailboxConfig config);
}