using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using iPath.Application.Features.Notifications;
using iPath.Domain.Entities;
using iPath.EF.Core.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace iPath.API.Services.Notifications;

public interface ISseConnectionManager
{
    Task AddConnectionAsync(Guid userId, HttpResponse response, CancellationToken ct);
    Task SendToUserAsync(Guid userId, string eventType, object payload, string? id = null);
    Task SendToGroupMembersAsync(Guid groupId, string eventType, object payload, string? id = null);
    Task BroadcastAsync(string eventType, object payload, string? id = null);
}

public class SseConnectionManager(IServiceProvider services, ILogger<SseConnectionManager> logger)
    : ISseConnectionManager
{
    private readonly ConcurrentDictionary<Guid, List<SseConnection>> _connections = new();

    public async Task AddConnectionAsync(Guid userId, HttpResponse response, CancellationToken ct)
    {
        var connectionId = Guid.NewGuid();
        var channel = Channel.CreateUnbounded<SseMessage>();
        var connection = new SseConnection(connectionId, channel);

        _connections.AddOrUpdate(userId,
            _ => [connection],
            (_, list) => { list.Add(connection); return list; });

        try
        {
            await foreach (var message in channel.Reader.ReadAllAsync(ct))
            {
                await WriteMessageAsync(response, message, ct);
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogDebug("SSE connection {ConnectionId} for user {UserId} cancelled", connectionId, userId);
        }
        finally
        {
            RemoveConnection(userId, connectionId);
        }
    }

    public async Task SendToUserAsync(Guid userId, string eventType, object payload, string? id = null)
    {
        if (!_connections.TryGetValue(userId, out var connections)) return;

        var data = JsonSerializer.Serialize(payload);
        var message = new SseMessage(eventType, data, id);
        foreach (var conn in connections.ToList())
        {
            try { await conn.Channel.Writer.WriteAsync(message); }
            catch (ChannelClosedException) { /* connection closing */ }
        }
    }

    public async Task SendToGroupMembersAsync(Guid groupId, string eventType, object payload, string? id = null)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<iPathDbContext>();

        var userIds = await db.Set<GroupMember>()
            .AsNoTracking()
            .Where(m => m.GroupId == groupId && m.Role != eMemberRole.Banned)
            .Select(m => m.UserId)
            .Distinct()
            .ToListAsync();

        var data = JsonSerializer.Serialize(payload);
        var message = new SseMessage(eventType, data, id);
        foreach (var userId in userIds)
        {
            if (!_connections.TryGetValue(userId, out var connections)) continue;
            foreach (var conn in connections.ToList())
            {
                try { await conn.Channel.Writer.WriteAsync(message); }
                catch (ChannelClosedException) { /* connection closing */ }
            }
        }
    }

    public async Task BroadcastAsync(string eventType, object payload, string? id = null)
    {
        var data = JsonSerializer.Serialize(payload);
        var message = new SseMessage(eventType, data, id);
        foreach (var kvp in _connections.ToList())
        {
            foreach (var conn in kvp.Value.ToList())
            {
                try { await conn.Channel.Writer.WriteAsync(message); }
                catch (ChannelClosedException) { /* connection closing */ }
            }
        }
    }

    private void RemoveConnection(Guid userId, Guid connectionId)
    {
        SseConnection? toClose = null;
        _connections.AddOrUpdate(userId,
            _ => [],
            (_, list) =>
            {
                toClose = list.FirstOrDefault(c => c.ConnectionId == connectionId);
                list.RemoveAll(c => c.ConnectionId == connectionId);
                return list;
            });

        toClose?.Channel.Writer.Complete();

        if (_connections.TryGetValue(userId, out var remaining) && remaining.Count == 0)
            _connections.TryRemove(userId, out _);
    }

    private static async Task WriteMessageAsync(HttpResponse response, SseMessage message, CancellationToken ct)
    {
        var sb = new StringBuilder();
        if (!string.IsNullOrEmpty(message.Id))
            sb.AppendLine($"id: {message.Id}");
        sb.AppendLine($"event: {message.EventType}");
        foreach (var line in message.Data.Split('\n'))
            sb.AppendLine($"data: {line}");
        sb.AppendLine();
        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        await response.Body.WriteAsync(bytes, ct);
        await response.Body.FlushAsync(ct);
    }
}

public class SseConnection(Guid connectionId, Channel<SseMessage> channel)
{
    public Guid ConnectionId { get; } = connectionId;
    public Channel<SseMessage> Channel { get; } = channel;
}
