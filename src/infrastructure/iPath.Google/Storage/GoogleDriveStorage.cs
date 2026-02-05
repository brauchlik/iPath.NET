using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using v3 = Google.Apis.Drive.v3;
using Google.Apis.Services;
using iPath.Application.Contracts;
using iPath.Domain.Entities;
using iPath.EF.Core.Database;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace iPath.Google.Storage;

public class GoogleDriveStorageService(IOptions<GoogleDriveConfig> opts,
    iPathDbContext db,
    IGroupCache groupCache,
    ILogger<GoogleDriveStorageService> logger)
    : IStorageService
{

    DriveService GDrive
    {
        get
        {
            if (field is null)
            {
                GoogleCredential credential;
                using (var stream = new FileStream(opts.Value.ClientSecretPath, FileMode.Open, FileAccess.Read))
                {
                    credential = GoogleCredential.FromStream(stream)
                        .CreateScoped(DriveService.Scope.Drive)
                        .CreateWithUser(opts.Value.Username); // For domain-wide delegation
                }

                field = new DriveService(new BaseClientService.Initializer
                {
                    HttpClientInitializer = credential,
                    ApplicationName = opts.Value.ApplicationName
                });
            }
            return field;
        }
    }



    public Task<StorageRepsonse> GetFileAsync(DocumentNode document, CancellationToken ctk = default)
    {
        throw new NotImplementedException();
    }

    public Task<StorageRepsonse> PutFileAsync(DocumentNode document, CancellationToken ctk = default)
    {
        throw new NotImplementedException();
    }

    public Task<StorageRepsonse> PutServiceRequestJsonAsync(ServiceRequest request, CancellationToken ctk = default)
    {
        throw new NotImplementedException();
    }

    private async Task<string?> GetOrCreateCommunityFolder(Guid CommunityId, CancellationToken ct = default)
    {
        var community = await db.Communities.FindAsync(CommunityId);
        if (string.IsNullOrEmpty(community.StorageId))
        {
            var storageId = await CreateOrGetFolderAsync(opts.Value.RootFolderId, community.Name, ct);
            community.StorageId = storageId;
            await db.SaveChangesAsync(ct);
        }
        return community.StorageId;
    }

    private async Task<string?> GetOrCreateGroupFolder(Guid groupId, CancellationToken ct = default)
    {
        var group = await db.Groups.FindAsync(groupId);
        if (group.CommunityId.HasValue && string.IsNullOrEmpty(group.StorageId))
        {
            // get community folder
            var communityStorageId = await GetOrCreateCommunityFolder(group.CommunityId.Value, ct);

            // create group folder
            var storageId = await CreateOrGetFolderAsync(communityStorageId, group.Name, ct);
            group.StorageId = storageId;
            await db.SaveChangesAsync(ct);
        }
        return group.StorageId;
    }

    private async Task<string> CreateOrGetFolderAsync(string parentId, string newFolderName, CancellationToken ct = default)
    {
        // Query for a folder with the given name in the parent folder
        string query = $"'{parentId}' in parents and name = '{newFolderName}' and mimeType = 'application/vnd.google-apps.folder' and trashed = false";
        FilesResource.ListRequest listRequest = GDrive.Files.List();
        listRequest.Q = query;
        listRequest.Fields = "files(id, name)";
        var result = await listRequest.ExecuteAsync(ct);

        if (result.Files != null && result.Files.Count > 0)
        {
            Console.WriteLine("folder already exists");
            return result.Files[0].Id;
        }

        // Create file metadata for the new folder
        var fileMetadata = new v3.Data.File
        {
            Name = newFolderName,
            MimeType = "application/vnd.google-apps.folder",
            Parents = new List<string> { parentId }
        };

        // Create the folder
        var createRequest = GDrive.Files.Create(fileMetadata);
        createRequest.Fields = "id";
        var folder = await createRequest.ExecuteAsync(ct);

        return folder.Id;
    }
}
