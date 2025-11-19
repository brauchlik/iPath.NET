using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace iPath.API.Services.Storage;

public class LocalChacheService(IOptions<iPathConfig> opts, ILogger<LocalChacheService> logger)
{
    public async Task<int> FilesCountAsync(CancellationToken ct)
    {
        var di = new System.IO.DirectoryInfo(opts.Value.TempDataPath);
        var task = new Func<Task<int>>(async () => di.GetFiles().Count());
        return await task();
    }

    public async Task<long> FolderSizeAsync(CancellationToken ct)
    {
        var di = new System.IO.DirectoryInfo(opts.Value.TempDataPath);
        var task = new Func<Task<long>>(async () => di.GetFiles().Select(f => f.Length).Sum());
        return await task();
    }

    public int CleanupFiles(DateTime maxDate)
    {
        int count = 0;
        var di = new System.IO.DirectoryInfo(opts.Value.TempDataPath);
        foreach( var fi in di.GetFiles() )
        {
            try
            {
                if (fi.LastAccessTime < maxDate)
                {
                    count++;
                    fi.Delete();
                }
            }
            catch(Exception ex)
            {
                logger.LogError(ex, ex.Message);
            }
        }
        return count;
    }
}