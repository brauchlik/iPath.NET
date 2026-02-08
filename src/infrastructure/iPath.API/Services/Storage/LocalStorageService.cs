using Ardalis.GuardClauses;
using iPath.EF.Core.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;

namespace iPath.API.Services.Storage;

public class LocalStorageService(IOptions<iPathConfig> opts, 
    iPathDbContext db, 
    ILogger<LocalStorageService> logger)
    : IRemoteStorageService
{
    public string ProviderName => "LocalFiles";

    public string StoragePath 
    {
        get
        {
            if( string.IsNullOrEmpty(field)) field = opts.Value.LocalDataPath;
            return field;
        }
    }


    public async Task<StorageRepsonse> GetFileAsync(Guid DocumentId, CancellationToken ct = default!)
    {
        try
        {
            var node = await db.Documents.AsNoTracking()
                .Include(d => d.ServiceRequest)
                .AsNoTracking()
                .FirstOrDefaultAsync(n => n.Id == DocumentId, ct);

            Guard.Against.NotFound(DocumentId, node);
            return await GetFileAsync(node, ct);
        }
        catch (Exception ex)
        {
            var msg = string.Format("Error getting NodeFile {0}: {1}", DocumentId, ex.Message);
            logger.LogError(msg);
            return new StorageRepsonse(false, Message: msg);
        }
    }

    public async Task<StorageRepsonse> GetFileAsync(DocumentNode document, CancellationToken ct = default!)
    {
        try
        {
            Guard.Against.Null(document);

            if (document.ServiceRequest is null)
                return new StorageRepsonse(false, "Document node does not belong to a group");

            if (string.IsNullOrEmpty(document.StorageId)) throw new Exception("File does not have a StorageId. It has not been previously exported to storage");

            var filePath = Path.Combine(GetServiceRequestPath(document.ServiceRequest), document.StorageId);
            if (!File.Exists(filePath)) throw new Exception($"File not found: {filePath}");

            // copy to local file
            var localFile = Path.Combine(opts.Value.TempDataPath, document.Id.ToString());
            if (!File.Exists(localFile)) File.Delete(localFile);
            File.Copy(filePath, localFile);

            logger.LogInformation($"Node {0} retrieved", document.Id);

            return new StorageRepsonse(true);

        }
        catch (Exception ex)
        {
            var msg = string.Format("Error getting NodeFile {0}: {1}", document?.Id, ex.Message);
            logger.LogError(msg);
            return new StorageRepsonse(false, Message: msg);
        }
    }

    public async Task<StorageRepsonse> PutFileAsync(Guid DocumentId, CancellationToken ct = default!)
    {
        try
        {
            var document = await db.Documents
                .Include(n => n.ServiceRequest)
                .FirstOrDefaultAsync(n => n.Id == DocumentId, ct);
            Guard.Against.NotFound(DocumentId, document);
            return await PutFileAsync(document, ct);
        }
        catch (Exception ex)
        {
            var msg = string.Format("Error putting NodeFile {0}: {1}", DocumentId, ex.Message);
            logger.LogError(msg);
            return new StorageRepsonse(false, Message: msg);
        }
    }

    public async Task<StorageRepsonse> PutFileAsync(DocumentNode document, CancellationToken ct = default!)
    {
        try
        {
            Guard.Against.Null(document);

            if (document.ServiceRequest is null) throw new Exception("ServiceRequest does not beldong to a group");

            if (string.IsNullOrEmpty(document.StorageId))
            {
                // create a new storygeId
                document.StorageId = Guid.CreateVersion7().ToString();
            }

            // check local file in temp
            var localFile = Path.Combine(opts.Value.TempDataPath, document.Id.ToString());
            if (!File.Exists(localFile)) throw new Exception($"Local file not found: {localFile}");

            var fn = Path.Combine(GetServiceRequestPath(document.ServiceRequest), document.StorageId);

            // delete storage file if exists
            if (File.Exists(fn)) File.Delete(fn);

            // copy tmp file to storgae
            File.Copy(localFile, fn);

            // save node
            document.File.LastStorageExportDate = DateTime.UtcNow;
            db.Documents.Update(document);
            await db.SaveChangesAsync(ct);

            return new StorageRepsonse(true, StorageId: document.StorageId);

        }
        catch (Exception ex)
        {
            var msg = string.Format("Error putting NodeFile {0}: {1}", document?.Id, ex.Message);
            logger.LogError(msg);
            return new StorageRepsonse(false, Message: msg);
        }
    }




    public async Task<StorageRepsonse> PutServiceRequestJsonAsync(Guid Id, CancellationToken ctk = default!)
    {
        try
        {
            var node = await db.ServiceRequests.AsNoTracking()
                .Include(n => n.Documents)
                .Include(n => n.Annotations)
                .FirstOrDefaultAsync(n => n.Id == Id, ctk);

            return await PutServiceRequestJsonAsync(node, ctk);
        }
        catch (Exception ex)
        {
            var msg = string.Format("Error putting NodeFile {0}: {1}", Id, ex.Message);
            logger.LogError(msg);
            return new StorageRepsonse(false, Message: msg);
        }
    }


    public async Task<StorageRepsonse> PutServiceRequestJsonAsync(ServiceRequest node, CancellationToken ctk = default!)
    {
        var jsonOpts = new JsonSerializerOptions()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
            ReferenceHandler = ReferenceHandler.IgnoreCycles,
            WriteIndented = true
        };

        var fn = Path.Combine(GetServiceRequestPath(node), $"{node.Id}.json");
        var str = JsonSerializer.Serialize(node, jsonOpts);
        await File.WriteAllTextAsync(fn, str, ctk);
        return new StorageRepsonse(true);
    }

    public async Task RenameRequest(ServiceRequest request)
    {
        // do nothing
    }

    public async Task RenameGroup(Group group)
    {
        // do nothing
    }

    public async Task RenameCommunity(Community community)
    {
        // do nothing
    }

    private string GetServiceRequestPath(ServiceRequest node)
    {
        if( !Directory.Exists(StoragePath) ) throw new Exception("Root directory for local storage not found");

        var dir = Path.Combine(StoragePath, node.GroupId.ToString());
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

        dir = Path.Combine(dir, node.Id.ToString());
        if( !Directory.Exists(dir)) Directory.CreateDirectory(dir); 

        return dir;
    }

    public Task<string?> CreateViewLink(DocumentNode doc, CancellationToken ct = default)
    {
        return null;
    }



    public Task<StorageRepsonse> DeleteFileAsync(Guid Id, CancellationToken ctk = default)
    {
        throw new NotImplementedException();
    }

    public Task<StorageRepsonse> DeleteServiceRequestJsonAsync(Guid Id, CancellationToken ctk = default)
    {
        throw new NotImplementedException();
    }



    #region "-- Upload Folder --


    public bool UserUploadFolderActive => false;

    public Task<UserUploadFolder> CreateUserUploadFolderAsync(Guid userId, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public Task DeleteUserUploadFolderAsync(Guid userId, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public Task<ServiceRequestUploadFolder> CreateRequestUploadFolderAsync(Guid ServiceRequestId, Guid UserId, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public Task DeleteRequestUploadFolderAsync(Guid FolderId, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public Task<ScanExternalDocumentResponse> ScanNewFilesAsync(ServiceRequestUploadFolder folder, CancellationToken ctk = default)
    {
        throw new NotImplementedException();
    }

    public Task ImportNewFilesAsync(ServiceRequestUploadFolder folder, IReadOnlyList<string> storageIds, CancellationToken ctk = default)
    {
        throw new NotImplementedException();
    }
    #endregion
}
