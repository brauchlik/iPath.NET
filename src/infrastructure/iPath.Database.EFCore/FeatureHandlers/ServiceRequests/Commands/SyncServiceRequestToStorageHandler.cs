using iPath.Application.Features.ServiceRequests.Commands;
using iPath.Domain.Config;
using Microsoft.Extensions.Options;

namespace iPath.EF.Core.FeatureHandlers.ServiceRequests.Commands;

public class SyncServiceRequestToStorageHandler(iPathDbContext db,
    IRemoteStorageUploadQueue queue,
    IOptions<iPathConfig> opts,
    ILogger<SyncServiceRequestToStorageHandler> logger)
    : IRequestHandler<SyncServiceRequestToStorageCommand, Task>
{
    public async Task Handle(SyncServiceRequestToStorageCommand request, CancellationToken ct)
    {
        if (request.DocumentId.HasValue)
        {
            var document = await db.Documents.FindAsync(request.DocumentId.Value, ct);
            Guard.Against.NotFound(request.DocumentId.Value, document);
            await Enqueue(document, ct);
        }
        else if (request.ServiceRequestId.HasValue)
        {

            var serviceRequest = await db.ServiceRequests.AsNoTracking()
                .Include(x => x.Documents)
                .SingleOrDefaultAsync(x => x.Id == request.ServiceRequestId.Value, ct);
            Guard.Against.NotFound(request.ServiceRequestId.Value, serviceRequest);

            // check if document file is in local storage and run upload again
            if (request.AllDocuments)
            {
                foreach (var d in serviceRequest.Documents)
                {
                    await Enqueue(d, ct);
                }
            }
            var srCmd = new RemoteStorageCommand(serviceRequest.Id, eRemoteStorageCommand.UploadServiceRequest);
            await queue.EnqueueAsync(srCmd, ct);
        }
    }

    private async Task Enqueue(DocumentNode document, CancellationToken ct)
    {
        var localFile = Path.Combine(opts.Value.TempDataPath, document.Id.ToString());
        if (!System.IO.File.Exists(localFile))
        {
            logger.LogWarning("Local file not found: {localFile}", localFile);
        }
        else
        {
            if (document.File.Storage is not null)
                logger.LogWarning("Local file {localFile} was synced before to {storage}", localFile, document.File.Storage);

            var dCmd = new RemoteStorageCommand(document.Id, eRemoteStorageCommand.UploadDocument);
            await queue.EnqueueAsync(dCmd, ct);
        }
    }
}
