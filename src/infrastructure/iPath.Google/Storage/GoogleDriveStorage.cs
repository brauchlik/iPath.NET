using Ardalis.GuardClauses;
using FluentResults;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Drive.v3.Data;
using Google.Apis.Services;
using iPath.Application;
using iPath.Application.Contracts;
using iPath.Application.Features.ServiceRequests;
using iPath.Domain.Config;
using iPath.Domain.Entities;
using iPath.EF.Core.Database;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Upload = Google.Apis.Upload;
using v3 = Google.Apis.Drive.v3;

namespace iPath.Google.Storage;

public class GoogleDriveStorageService(IOptions<GoogleDriveConfig> gdriveOpts,
    IOptions<iPathConfig> opts,
    IOptions<iPathClientConfig> clientOpts,
    iPathDbContext db,
    UserManager<Domain.Entities.User> um,
    IMimetypeService mime,
    IGroupCache groupCache,
    ILogger<GoogleDriveStorageService> logger)
    : IRemoteStorageService
{

    public string ProviderName => "GoogleDrive";

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


    private async Task<DocumentNode?> GetDocument(Guid Id, CancellationToken ct = default)
    {
        return await db.Documents
            .Include(d => d.ServiceRequest)
            .SingleOrDefaultAsync(x => x.Id == Id, ct);
    }

    public async Task<StorageRepsonse> GetFileAsync(Guid Id, CancellationToken ct = default)
        => await GetFileAsync(await GetDocument(Id, ct), ct);

    private async Task<StorageRepsonse> GetFileAsync(DocumentNode? document, CancellationToken ct = default)
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
                if (System.IO.File.Exists(localFile)) System.IO.File.Delete(localFile);

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

    public Task<StorageRepsonse> DeleteFileAsync(Guid Id, CancellationToken ctk = default)
    {
        throw new NotImplementedException();
    }

    public async Task<StorageRepsonse> PutFileAsync(Guid Id, CancellationToken ct = default)
        => await PutFileAsync(await GetDocument(Id, ct), ct);

    private async Task<StorageRepsonse> PutFileAsync(DocumentNode? document, CancellationToken ct = default)
    {
        try
        {
            Guard.Against.Null(document);

            if (!string.IsNullOrEmpty(document.StorageId))
                return StorageRepsonse.Fail("document has already been stored");

            // check local file in temp
            var localFile = Path.Combine(opts.Value.TempDataPath, document.Id.ToString());
            if (!System.IO.File.Exists(localFile))
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
                document.File.PublicUrl = await CreateViewLink(document, ct);
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




    private async Task<ServiceRequest?> GetRequest(Guid id, CancellationToken ct)
        => await db.ServiceRequests
                        .Include(d => d.Documents)
                        .Include(d => d.Annotations)
                        .SingleOrDefaultAsync(x => x.Id == id, ct);

    public async Task<StorageRepsonse> PutServiceRequestJsonAsync(Guid Id, CancellationToken ct = default)
        => await PutServiceRequestJsonAsync(await GetRequest(Id, ct), ct);

    public Task<StorageRepsonse> PutServiceRequestJsonAsync(ServiceRequest? request, CancellationToken ctk = default)
    {
        throw new NotImplementedException();
    }

    public Task<StorageRepsonse> DeleteServiceRequestJsonAsync(Guid Id, CancellationToken ctk = default)
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

    private string RequestFolderName(ServiceRequest sr) => sr.CreatedOn.ToString("yyyy-MM-dd") + " - " + sr.Description.FullTitle();

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

    private async Task ShareFolderWithUserAsync(string folderId, string userEmail, string role = "writer", CancellationToken ct = default)
    {
        var userPermission = new Permission
        {
            Type = "user",
            Role = role, // "writer", "reader", or "commenter"
            EmailAddress = userEmail
        };

        var request = GDrive.Permissions.Create(userPermission, folderId);
        request.SendNotificationEmail = true; // This triggers the invitation email
        request.EmailMessage = """
A folder to upload images to iPath.NET has been created for you on google workspace. 
You may include this folder into your personal google drive space.

When you create new requests on iPath, the server can now create a fodler for each new request
and you can upload images and other files directly into that folder. From there it will be importaed automatically into the iPath system.
""";
        await request.ExecuteAsync(ct);
    }

    private async Task DeleteFolderAsync(string folderId, CancellationToken ct = default)
    {
        // List all items in the folder
        FilesResource.ListRequest listRequest = GDrive.Files.List();
        listRequest.Q = $"'{folderId}' in parents and trashed = false";
        listRequest.Fields = "files(id, mimeType)";
        var result = await listRequest.ExecuteAsync(ct);

        if (result.Files != null)
        {
            foreach (var item in result.Files)
            {
                if (item.MimeType == "application/vnd.google-apps.folder")
                {
                    // Recursively delete subfolder
                    await DeleteFolderAsync(item.Id, ct);
                }
                else
                {
                    // Delete file
                    await GDrive.Files.Delete(item.Id).ExecuteAsync(ct);
                }
            }
        }

        // Delete the folder itself
        await GDrive.Files.Delete(folderId).ExecuteAsync(ct);
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

    public async Task<string?> CreateViewLink(DocumentNode doc, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(doc.StorageId)) return null;

        // Permission logic remains the same
        var newPermission = new Permission
        {
            Type = "anyone",
            Role = "reader"
        };
        await GDrive.Permissions.Create(newPermission, doc.StorageId).ExecuteAsync();


        // create a preview link
        var request = GDrive.Files.Get(doc.StorageId);
        // Request both thumbnail and the original link as a fallback
        request.Fields = "thumbnailLink, webContentLink";
        var file = await request.ExecuteAsync();

        if (!string.IsNullOrEmpty(file.ThumbnailLink))
        {
            // Google links usually end in =s220 or =w200-h200
            // We use Regex or IndexOf to safely swap the ending
            string baseUrl = file.ThumbnailLink;
            int index = baseUrl.LastIndexOf('=');

            if (index > 0)
            {
                baseUrl = baseUrl.Substring(0, index);
            }

            // Use =s1280 for the size. 
            // Ensure there are no trailing spaces or weird characters.
            return $"{baseUrl}=s1280";
        }

        // Fallback: If no thumbnail exists, we might have to use the direct link
        return file.WebContentLink;

        // alternative: https://drive.google.com/uc?export=view&id={StorageId}
        return $"https://drive.google.com/uc?export=view&id={doc.StorageId}";
    }




    public async Task<ScanExternalDocumentResponse> ScanNewFilesAsync(Guid requestId, CancellationToken ctk = default!)
    {
        var sr = await db.ServiceRequests
            .AsNoTracking()
            .Include(sr => sr.Documents)
            .SingleOrDefaultAsync(x => x.Id == requestId, ctk);

        if (sr?.StorageId is null) return new ScanExternalDocumentResponse("Google", null);

        FilesResource.ListRequest listRequest = GDrive.Files.List();
        listRequest.Q = $"'{sr.StorageId}' in parents and trashed = false";
        listRequest.Fields = "nextPageToken, files(id, name, mimeType, owners, size, createdTime)";

        IList<v3.Data.File> items = listRequest.Execute().Files;

        List<ExternalFile> newitems = new();

        Console.WriteLine("Items in folder:");
        if (items != null && items.Count > 0)
        {
            foreach (var item in items)
            {
                if (item.MimeType != "application/vnd.google-apps.folder")
                {
                    if (!sr.Documents.Any(d => d.StorageId == item.Id))
                    {
                        var newItem = new ExternalFile(StorageId: item.Id, Filename: item.Name, Mimetype: item.MimeType,
                            FileSize: item.Size, CreatedOn: item.CreatedTimeDateTimeOffset);
                        newitems.Add(newItem);
                    }
                }
            }
        }

        return new ScanExternalDocumentResponse("Google", newitems);
    }


    public async Task ImportNewFilesAsync(Guid requestId, IReadOnlyList<string> storageIds, CancellationToken ctk = default!)
    {
        var sr = await db.ServiceRequests
            .Include(sr => sr.Documents)
            .SingleOrDefaultAsync(x => x.Id == requestId, ctk);

        if (sr?.StorageId is null) return;

        FilesResource.ListRequest listRequest = GDrive.Files.List();
        listRequest.Q = $"'{sr.StorageId}' in parents and trashed = false";
        listRequest.Fields = "nextPageToken, files(id, name, mimeType, owners)";

        IList<v3.Data.File> items = listRequest.Execute().Files;
        List<v3.Data.File> newitems = new();

        Console.WriteLine("Items in folder:");
        if (items != null && items.Count > 0)
        {
            foreach (var item in items)
            {
                if (item.MimeType != "application/vnd.google-apps.folder" && storageIds.Contains(item.Id))
                {
                    if (!sr.Documents.Any(d => d.StorageId == item.Id))
                    {
                        newitems.Add(item);
                        var newDoc = new DocumentNode
                        {
                            Id = Guid.CreateVersion7(),
                            ServiceRequestId = sr.Id,
                            CreatedOn = DateTime.UtcNow,
                            OwnerId = sr.OwnerId,
                            SortNr = sr.Documents.Max(x => x.SortNr) + 1,
                            StorageId = item.Id,
                            DocumentType = "file",
                            File = new NodeFile
                            {
                                Filename = item.Name,
                                MimeType = item.MimeType
                            }
                        };
                        await db.Documents.AddAsync(newDoc, ctk);

                        if (mime.IsImage(item.Name))
                        {
                            // thumnail
                            newDoc.File.ThumbData = await GetThumbnailBase64Async(item.Id);
                            newDoc.DocumentType = "image";
                        }
                        if (clientOpts.Value.WsiExtensions.Contains(System.IO.Path.GetExtension(item.Name)))
                        {
                            // 
                            newDoc.File.PublicUrl = await CreatePublicRangeLinkAsync(item.Id, ctk);
                            newDoc.DocumentType = "wsi";
                        }
                        else
                        {
                            // view link for images & files
                            newDoc.File.PublicUrl = await CreateViewLink(newDoc, ctk);
                        }
                    }
                }
            }
            await db.SaveChangesAsync(ctk);
        }
    }


    private async Task<string?> GetThumbnailBase64Async(string fileId, CancellationToken ct = default)
    {
        // Get the thumbnail link from Google Drive
        var request = GDrive.Files.Get(fileId);
        request.Fields = "thumbnailLink";
        var file = await request.ExecuteAsync(ct);

        if (string.IsNullOrEmpty(file.ThumbnailLink))
            return null;

        // Adjust the thumbnail URL to request 120x120 pixels
        var baseUrl = file.ThumbnailLink;
        int index = baseUrl.LastIndexOf('=');
        if (index > 0)
            baseUrl = baseUrl.Substring(0, index);
        var thumbnailUrl = $"{baseUrl}=s" + clientOpts.Value.ThumbSize;

        // Download the thumbnail image
        using var httpClient = new HttpClient();
        var imageBytes = await httpClient.GetByteArrayAsync(thumbnailUrl, ct);

        // Convert to base64 string
        return Convert.ToBase64String(imageBytes);
    }

    private async Task<string> CreatePublicRangeLinkAsync(string fileId, CancellationToken ct = default)
    {
        // 1. Make the file public (Anyone with link can view)
        var publicPermission = new Permission
        {
            Type = "anyone",
            Role = "reader"
        };

        await GDrive.Permissions.Create(publicPermission, fileId).ExecuteAsync(ct);

        // 2. Generate the Direct Download URL
        // This format allows HTTP Range requests (206 Partial Content)
        // Format: https://www.googleapis.com/drive/v3/files/{fileId}?alt=media&key={YOUR_API_KEY}
        // Note: For truly public access without OAuth tokens in the header, 
        // you usually append an API Key or use a specific proxy URL.

        string directUrl = $"https://www.googleapis.com/drive/v3/files/{fileId}?alt=media&key={gdriveOpts.Value.PUBLIC_API_KEY}";
        return directUrl;
    }





    #region "-- Upload Folder --
    public bool UserUploadFolderActive => !string.IsNullOrEmpty(gdriveOpts.Value.UserUploadFolderId);



    public async Task<UserUploadFolder> CreateUserUploadFolderAsync(Guid userId, CancellationToken ct)
    {
        var user = await db.Users
            .Include(u => u.UploadFolders)
            .SingleOrDefaultAsync(u => u.Id == userId, ct);
        Guard.Against.NotFound(userId, user);

        // validate that the user is linked to a google account
        var logins = await um.GetLoginsAsync(user);
        if (!logins.Any(x => x.LoginProvider == "Google"))
        {
            throw new Exception("user account is not linked to a google login");
        }

        var folder = user.UploadFolders.FirstOrDefault(x => x.StorageProvider == this.ProviderName);
        if (folder == null)
        {
            var gDir = await CreateOrGetFolderAsync(gdriveOpts.Value.UserUploadFolderId, user.UserName);

            // TODO: pleace a instrctions.txt file inside the users upload folder.

            await ShareFolderWithUserAsync(gDir, user.Email, ct: ct);

            var uFolder = UserUploadFolder.Create(user.Id, this.ProviderName, gDir);
            await db.UserUploadFolders.AddAsync(uFolder, ct);
            await db.SaveChangesAsync(ct);
        }
        return folder;
    }

    public async Task DeleteUserUploadFolderAsync(Guid userId, CancellationToken ct)
    {
        var user = await db.Users.Include(u => u.UploadFolders)
            .SingleOrDefaultAsync(u => u.Id == userId, ct);
        Guard.Against.NotFound(userId, user);

        var folder = user.UploadFolders.FirstOrDefault(x => x.StorageProvider == this.ProviderName);
        if (folder != null)
        {
            await DeleteFolderAsync(folder.StorageId, ct);
            db.UserUploadFolders.Remove(folder);
            user.UploadFolders.Remove(folder);
            await db.SaveChangesAsync(ct);
        }
    }

    public async Task<ServiceRequestUploadFolder> CreateRequestUploadFolderAsync(Guid ServiceRequestId, Guid UserId, CancellationToken ct)
    {
        var sr = await db.ServiceRequests.
            AsNoTracking()
            .Include(x => x.UploadFolders)
            .FirstOrDefaultAsync(f => f.Id == ServiceRequestId, ct);
        Guard.Against.NotFound(ServiceRequestId, sr);

        if (sr.UploadFolders.Any())
        {
            return sr.UploadFolders.First();
        }
        else
        {
            var userFolder = await db.Set<UserUploadFolder>()
               .SingleOrDefaultAsync(f => f.UserId == UserId && f.StorageProvider == this.ProviderName, ct);
            if (userFolder is null)
            {
                throw new Exception("no uload folder for user account configured");
            }

            var folderName = sr.Description.FullTitle();
            var gDir = await CreateOrGetFolderAsync(userFolder.StorageId, folderName);

            var folder = ServiceRequestUploadFolder.Create(uploadFolderId: userFolder.Id, serviceRequestId: sr.Id, storageId: gDir);
            await db.ServiceRequestUploadFolders.AddAsync(folder, ct);
            await db.SaveChangesAsync(ct);
            return folder;
        }
    }

    public Task DeleteRequestUploadFolderAsync(Guid FolderId, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public Task<ScanExternalDocumentResponse> ScanUploadFolderAsync(ServiceRequestUploadFolder folder, CancellationToken ctk = default)
    {
        throw new NotImplementedException();
    }

    public async Task<int> ImportUploadFolderAsync(ServiceRequestUploadFolder folder, IReadOnlyList<string>? storageIds, CancellationToken ct = default)
    {
        // scan the folder with Id = folder.StorageId on google drive and list all files with name, id and mimetype

        FilesResource.ListRequest listRequest = GDrive.Files.List();
        listRequest.Q = $"'{folder.StorageId}' in parents and trashed = false";
        listRequest.Fields = "nextPageToken, files(id, name, mimeType)";

        var result = await listRequest.ExecuteAsync(ct);

        int newCount = 0;
        if (result.Files != null && result.Files.Count > 0)
        {
            foreach (var item in result.Files)
            {
                if (item.MimeType != "application/vnd.google-apps.folder")
                {
                    // filter if the file is in import list
                    if (storageIds is null || storageIds.Contains(item.Id))
                    {
                        // filter already imported
                        if (!folder.ServiceRequest.Documents.Any(d => d.StorageId == item.Id))
                        {
                            newCount++;
                            var newDoc = new DocumentNode
                            {
                                Id = Guid.CreateVersion7(),
                                ServiceRequestId = folder.ServiceRequestId,
                                CreatedOn = DateTime.UtcNow,
                                OwnerId = folder.ServiceRequest.OwnerId,
                                SortNr = folder.ServiceRequest.Documents.IsEmpty() ? 0 : folder.ServiceRequest.Documents.Max(x => x.SortNr) + 1,
                                StorageId = item.Id,
                                DocumentType = "file",
                                File = new NodeFile
                                {
                                    Filename = item.Name,
                                    MimeType = item.MimeType
                                }
                            };
                            await db.Documents.AddAsync(newDoc, ct);

                            if (mime.IsImage(item.Name))
                            {
                                // thumnail
                                newDoc.File.ThumbData = await GetThumbnailBase64Async(item.Id);
                                newDoc.DocumentType = "image";
                            }
                            if (clientOpts.Value.WsiExtensions.Contains(System.IO.Path.GetExtension(item.Name)))
                            {
                                // 
                                newDoc.File.PublicUrl = await CreatePublicRangeLinkAsync(item.Id, ct);
                                newDoc.DocumentType = "wsi";
                            }
                            else
                            {
                                // view link for images & files
                                newDoc.File.PublicUrl = await CreateViewLink(newDoc, ct);
                            }
                        }
                    }
                }
            }
            await db.SaveChangesAsync(ct);
        }
        return newCount;

    }
    #endregion
}
