using Ardalis.GuardClauses;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using iPath.Application.Contracts;
using iPath.Domain.Config;
using iPath.Domain.Entities;
using iPath.EF.Core.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using iPath.Application.Features.ServiceRequests;
using v3 = Google.Apis.Drive.v3;
using Upload = Google.Apis.Upload;

namespace iPath.Google.Storage;

public class GoogleDriveStorageService(IOptions<GoogleDriveConfig> gdriveOpts,
    IOptions<iPathConfig> opts,
    iPathDbContext db,
    IGroupCache groupCache,
    ILogger<GoogleDriveStorageService> logger)
    : IRemoteStorageService
{

    DriveService GDrive
    {
        get
        {
            if (field is null)
            {
                GoogleCredential credential;
                using (var stream = new FileStream(gdriveOpts.Value.ClientSecretPath, FileMode.Open, FileAccess.Read))
                {
                    credential = GoogleCredential.FromStream(stream)
                        .CreateScoped(DriveService.Scope.Drive)
                        .CreateWithUser(gdriveOpts.Value.Username); // For domain-wide delegation
                }

                field = new DriveService(new BaseClientService.Initializer
                {
                    HttpClientInitializer = credential,
                    ApplicationName = gdriveOpts.Value.ApplicationName
                });
            }
            return field;
        }
    }



    public async Task<StorageRepsonse> GetFileAsync(DocumentNode document, CancellationToken ct = default)
    {
        try
        {
            Guard.Against.Null(document);
            if (!string.IsNullOrEmpty(document.StorageId))
            {
                var stream = new MemoryStream();
                var request = GDrive.Files.Get(document.StorageId);
                await request.DownloadAsync(stream, ct);
                stream.Position = 0; // Reset stream position for reading

                // copy to local file
                var localFile = Path.Combine(opts.Value.TempDataPath, document.Id.ToString());
                if (File.Exists(localFile)) File.Delete(localFile);

                using var fileStream = new FileStream(localFile, FileMode.Create, FileAccess.Write);
                await stream.CopyToAsync(fileStream, ct);
                return new StorageRepsonse(true);
            }
            return new StorageRepsonse(false, Message: "document has no storageId");
        }
        catch (Exception ex)
        {
            var msg = string.Format("Error getting NodeFile {0}: {1}", document?.Id, ex.Message);
            logger.LogError(msg);
            return new StorageRepsonse(false, Message: msg);
        }
    }

    public async Task<StorageRepsonse> PutFileAsync(DocumentNode document, CancellationToken ct = default)
    {
        try
        {
            if (!string.IsNullOrEmpty(document.StorageId))
                return StorageRepsonse.Fail("document has already been stored");

            // check local file in temp
            var localFile = Path.Combine(opts.Value.TempDataPath, document.Id.ToString());
            if (!File.Exists(localFile))
                return StorageRepsonse.Fail($"Local file not found: {localFile}");

            if (document.ServiceRequest?.GroupId is null)
                return StorageRepsonse.Fail("ServiceRequest does not beldong to a group");

            var requestStorageid = await GetOrCreateServiceRequestFolder(document.ServiceRequest.Id);

            // upload the file
            var fileMetadata = new v3.Data.File
            {
                Name = document.File.Filename,
                MimeType = document.File.MimeType,
                Parents = new List<string> { requestStorageid }
            };

            using var fileStream = new FileStream(localFile, FileMode.Open, FileAccess.Read);
            var request = GDrive.Files.Create(
                fileMetadata,
                fileStream,
                document.File.MimeType
            );
            request.Fields = "id";
            var file = await request.UploadAsync(ct);

            if (file.Status == Upload.UploadStatus.Completed)
            {
                document.StorageId = request.ResponseBody.Id;
                document.File.LastStorageExportDate = DateTime.UtcNow;
                db.Documents.Update(document);
                await db.SaveChangesAsync(ct);

                return StorageRepsonse.Ok(document.StorageId);
            }
            else
            {
                return StorageRepsonse.Fail($"File upload failed: {file.Exception?.Message}");
            }
        }
        catch (Exception ex)
        {
            var msg = string.Format("Error putting NodeFile {0}: {1}", document?.Id, ex.Message);
            logger.LogError(msg);
            return new StorageRepsonse(false, Message: msg);
        }
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
            var storageId = await CreateOrGetFolderAsync(gdriveOpts.Value.RootFolderId, community.Name, ct);
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

    private async Task<string?> GetOrCreateServiceRequestFolder(Guid requestId, CancellationToken ct = default)
    {
        var sr = await db.ServiceRequests.FindAsync(requestId);
        if (string.IsNullOrEmpty(sr.StorageId))
        {
            // get group folder
            var groupStorageId = await GetOrCreateGroupFolder(sr.GroupId, ct);

            // create request folder
            var requestFolderName = RequestFolderName(sr);
            var storageId = await CreateOrGetFolderAsync(groupStorageId, requestFolderName, ct);
            sr.StorageId = storageId;
            await db.SaveChangesAsync(ct);
        }
        return sr.StorageId;
    }

    private string RequestFolderName(ServiceRequest sr) =>  sr.CreatedOn.ToString("yyyy-MM-dd") + " - " +sr.Description.FullTitle();

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

    public async Task RenameRequest(ServiceRequest request)
    {
        if (!string.IsNullOrEmpty(request.StorageId))
        {
            await RenameFolder(request.StorageId, RequestFolderName(request));
        }
    }

    public async Task RenameGroup(Group group)
    {
        if (!string.IsNullOrEmpty(group.StorageId))
        {
            await RenameFolder(group.StorageId, group.Name);
        }
    }

    public async Task RenameCommunity(Community community)
    {
        if (!string.IsNullOrEmpty(community.StorageId))
        {
            await RenameFolder(community.StorageId, community.Name);
        }
    }

    private async Task RenameFolder(string id, string name)
    {
        try
        {
            var fileMetadata = new v3.Data.File
            {
                Name = name
            };

            var updateRequest = GDrive.Files.Update(fileMetadata, id);
            updateRequest.Fields = "id, name";
            await updateRequest.ExecuteAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, ex.Message);
        }
    }
}
