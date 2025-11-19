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
    : IStorageService
{

    private string _storagePath;
    public string StoragePath 
    {
        get
        {
            if( string.IsNullOrEmpty(_storagePath)) _storagePath = opts.Value.LocalDataPath;
            return _storagePath;
        }
    }


    public async Task<StorageRepsonse> GetNodeFileAsync(Guid NodeId, CancellationToken ct = default!)
    {
        try
        {
            var node = await db.Nodes.AsNoTracking()
                .Include(n => n.RootNode)
                .FirstOrDefaultAsync(n => n.Id == NodeId, ct);

            Guard.Against.NotFound(NodeId, node);
            return await GetNodeFileAsync(node, ct);
        }
        catch (Exception ex)
        {
            var msg = string.Format("Error getting NodeFile {0}: {1}", NodeId, ex.Message);
            logger.LogError(msg);
            return new StorageRepsonse(false, Message: msg);
        }
    }

    public async Task<StorageRepsonse> GetNodeFileAsync(Node node, CancellationToken ct = default!)
    {
        try
        {
            Guard.Against.Null(node);

            if (node.RootNode is null || !node.RootNode.GroupId.HasValue)
                return new StorageRepsonse(false, "Root node does not beldong to a group");

            if (string.IsNullOrEmpty(node.StorageId)) throw new Exception("File does not have a StorageId. It has not been previously exported to storage");

            var filePath = Path.Combine(GetNodePath(node.RootNode), node.StorageId);
            if (!File.Exists(filePath)) throw new Exception($"File not found: {filePath}");

            // copy to local file
            var localFile = Path.Combine(opts.Value.TempDataPath, node.Id.ToString());
            if (!File.Exists(localFile)) File.Delete(localFile);
            File.Copy(filePath, localFile);

            logger.LogInformation($"Node {0} retrieved", node.Id);

            return new StorageRepsonse(true);

        }
        catch (Exception ex)
        {
            var msg = string.Format("Error getting NodeFile {0}: {1}", node?.Id, ex.Message);
            logger.LogError(msg);
            return new StorageRepsonse(false, Message: msg);
        }
    }

    public async Task<StorageRepsonse> PutNodeFileAsync(Guid NodeId, CancellationToken ct = default!)
    {
        try
        {
            var node = await db.Nodes
                .Include(n => n.RootNode)
                .FirstOrDefaultAsync(n => n.Id == NodeId, ct);
            Guard.Against.NotFound(NodeId, node);
            return await PutNodeFileAsync(node, ct);
        }
        catch (Exception ex)
        {
            var msg = string.Format("Error putting NodeFile {0}: {1}", NodeId, ex.Message);
            logger.LogError(msg);
            return new StorageRepsonse(false, Message: msg);
        }
    }

    public async Task<StorageRepsonse> PutNodeFileAsync(Node node, CancellationToken ct = default!)
    {
        try
        {
            Guard.Against.Null(node);

            if (node.RootNode is null || !node.RootNode.GroupId.HasValue) throw new Exception("Root node does not beldong to a group");

            if (string.IsNullOrEmpty(node.StorageId))
            {
                // create a new storygeId
                node.StorageId = Guid.CreateVersion7().ToString();
            }

            // check local file in temp
            var localFile = Path.Combine(opts.Value.TempDataPath, node.Id.ToString());
            if (!File.Exists(localFile)) throw new Exception($"Local file not found: {localFile}");

            var fn = Path.Combine(GetNodePath(node.RootNode), node.StorageId);

            // delete storage file if exists
            if (File.Exists(fn)) File.Delete(fn);

            // copy tmp file to storgae
            File.Copy(localFile, fn);

            // save node
            node.File.LastStorageExportDate = DateTime.UtcNow;
            db.Nodes.Update(node);
            await db.SaveChangesAsync(ct);

            return new StorageRepsonse(true, StorageId: node.StorageId);

        }
        catch (Exception ex)
        {
            var msg = string.Format("Error putting NodeFile {0}: {1}", node?.Id, ex.Message);
            logger.LogError(msg);
            return new StorageRepsonse(false, Message: msg);
        }
    }



    public async Task<StorageRepsonse> PutNodeJsonAsync(Guid NodeId, CancellationToken ctk = default!)
    {
        try
        {
            var node = await db.Nodes.AsNoTracking()
                .Include(n => n.ChildNodes)
                .Include(n => n.Annotations)
                .FirstOrDefaultAsync(n => n.Id == NodeId, ctk);

            return await PutNodeJsonAsync(node, ctk);
        }
        catch (Exception ex)
        {
            var msg = string.Format("Error putting NodeFile {0}: {1}", NodeId, ex.Message);
            logger.LogError(msg);
            return new StorageRepsonse(false, Message: msg);
        }
    }


    public async Task<StorageRepsonse> PutNodeJsonAsync(Node node, CancellationToken ctk = default!)
    {
        var jsonOpts = new JsonSerializerOptions()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
            ReferenceHandler = ReferenceHandler.IgnoreCycles,
            WriteIndented = true
        };

        var fn = Path.Combine(GetNodePath(node), $"{node.Id}.json");
        var str = JsonSerializer.Serialize(node, jsonOpts);
        await File.WriteAllTextAsync(fn, str, ctk);
        return new StorageRepsonse(true);
    }

     


    private string GetNodePath(Node node)
    {
        if( !Directory.Exists(StoragePath) ) throw new Exception("Root directory for local storage not found");

        var dir = Path.Combine(StoragePath, node.GroupId.ToString());
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

        dir = Path.Combine(dir, node.Id.ToString());
        if( !Directory.Exists(dir)) Directory.CreateDirectory(dir); 

        return dir;
    }
}
