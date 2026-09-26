using System.Threading.Channels;
using iPath.Application.Contracts.Storage;

namespace iPath.API.Services.Storage.Migration;

public sealed class StorageMigrationQueue : IStorageMigrationQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions { SingleReader = true });

    public ValueTask EnqueueAsync(Guid migrationId, CancellationToken ct = default) => _channel.Writer.WriteAsync(migrationId, ct);

    public IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken ct) => _channel.Reader.ReadAllAsync(ct);
}
