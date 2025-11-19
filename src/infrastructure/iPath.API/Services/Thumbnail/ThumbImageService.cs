using DispatchR.Abstractions.Notification;
using iPath.EF.Core.Database;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace iPath.API.Services.Thumbnail;

public class ThumbImageService(IOptions<iPathConfig> opts,
    iPathDbContext db,
    IImageInfoService srv,
    ILogger<ThumbImageService> logger)
    : INotificationHandler<NodeThumnailNotCreatedNotification>, IThumbImageService
{
    public async ValueTask Handle(NodeThumnailNotCreatedNotification request, CancellationToken cancellationToken)
    {
        try
        {
            var node = await db.Nodes.FindAsync(request.nodeId, cancellationToken);
            if (node != null)
            {
                logger.LogInformation($"creating thumbnail for node {node.Id}");

                var fn = Path.Combine(opts.Value.TempDataPath, node.Id.ToString());
                if (System.IO.File.Exists(fn))
                {
                    await UpdateNodeAsync(node.File, fn);
                    await db.SaveChangesAsync(cancellationToken);
                }
            }
            else
            {
                logger.LogWarning($"node {node.Id} not found");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex.Message, ex);
        }
    }

    public async Task<NodeFile> UpdateNodeAsync(NodeFile file, string filename)
    {
        if (System.IO.File.Exists(filename))
        {
            try
            {
                var info = await srv.GetImageInfoAsync(filename);
                file.ImageWidth = info.width;
                file.ImageHeight = info.height;
                file.ThumbData = info.thumb;
            }
            catch (Exception ex)
            {
                logger.LogError(ex.Message, ex);
            }
        }
        else
        {
            logger.LogWarning($"File not found {filename}");
        }

        return file;
    }
}