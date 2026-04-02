using iPath.Application.Features.EmailImport;

namespace iPath.Application.Contracts;

public interface IMailBoxFactory
{
    IMailBox Create(ImapConfig config);
}