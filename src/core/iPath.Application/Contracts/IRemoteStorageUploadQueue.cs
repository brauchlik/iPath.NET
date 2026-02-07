namespace iPath.Application.Contracts;

public interface IRemoteStorageUploadQueue
{
    ValueTask EnqueueAsync(RemoteStorageCommand command, CancellationToken cancellationToken);

    ValueTask<RemoteStorageCommand> DequeueAsync(CancellationToken cancellationToken);

    int QueueSize { get; }
}


public record RemoteStorageCommand(Guid objId, eRemoteStorageCommand command)
{
    public bool IsDocumentCommand => command == eRemoteStorageCommand.DeleteDocument || command == eRemoteStorageCommand.FetchDocument || command == eRemoteStorageCommand.UploadDocument;
    public bool IsReqeustCommand => command == eRemoteStorageCommand.DeleteServiceRequest || command == eRemoteStorageCommand.UploadServiceRequest;
}


public enum eRemoteStorageCommand
{
    UploadDocument = 1,
    DeleteDocument = 2,
    FetchDocument = 3,
    UploadServiceRequest = 11,
    DeleteServiceRequest = 12,
}