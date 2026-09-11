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

/// <summary>
/// Fan-out addresses. Connections are bucketed by an opaque string rather than a user id so
/// that CaseRoom guests — who all share the synthetic principal <see cref="Guid.Empty"/> —
/// get one bucket each instead of landing in a single shared one.
/// </summary>
public static class SseChannel
{
    public static string User(Guid userId) => $"user:{userId}";

    public static string Guest(Guid requestId, Guid sessionId) => $"guest:{requestId}:{sessionId}";
}

public interface ISseConnectionManager
{
    Task AddConnectionAsync(string channel, HttpResponse response, CancellationToken ct);
    Task SendToChannelAsync(string channel, string eventType, object payload, string? id = null);
    Task SendToUserAsync(Guid userId, string eventType, object payload, string? id = null);
    Task SendToGroupMembersAsync(Guid groupId, string eventType, object payload, string? id = null);
    Task BroadcastAsync(string eventType, object payload, string? id = null);
}

public class SseConnectionManager(IServiceProvider services, ILogger<SseConnectionManager> logger)
    : ISseConnectionManager
{
    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private readonly ConcurrentDictionary<string, List<SseConnection>> _connections = new();
    private PeriodicTimer? _keepAliveTimer;
    private CancellationTokenSource? _keepAliveCts;

    public async Task AddConnectionAsync(string channel, HttpResponse response, CancellationToken ct)
    {
        var connectionId = Guid.NewGuid();
        var messages = Channel.CreateUnbounded<SseMessage>();
        var connection = new SseConnection(connectionId, messages);

        _connections.AddOrUpdate(channel,
            _ => [connection],
            (_, list) => { list.Add(connection); return list; });

        if (_connections.Count == 1)
            StartKeepAlive();

        try
        {
            await foreach (var message in messages.Reader.ReadAllAsync(ct))
            {
                await WriteMessageAsync(response, message, ct);
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogDebug("SSE connection {ConnectionId} on channel {Channel} cancelled", connectionId, channel);
        }
        finally
        {
            RemoveConnection(channel, connectionId);
        }
    }

    public async Task SendToChannelAsync(string channel, string eventType, object payload, string? id = null)
    {
        if (!_connections.TryGetValue(channel, out var connections)) return;

        var data = JsonSerializer.Serialize(payload, _jsonOptions);
        var message = new SseMessage(eventType, data, id);
        foreach (var conn in connections.ToList())
        {
            try { await conn.Channel.Writer.WriteAsync(message); }
            catch (ChannelClosedException) { /* connection closing */ }
        }
    }

    public Task SendToUserAsync(Guid userId, string eventType, object payload, string? id = null)
        => SendToChannelAsync(SseChannel.User(userId), eventType, payload, id);

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

        var data = JsonSerializer.Serialize(payload, _jsonOptions);
        var message = new SseMessage(eventType, data, id);
        foreach (var userId in userIds)
        {
            if (!_connections.TryGetValue(SseChannel.User(userId), out var connections)) continue;
            foreach (var conn in connections.ToList())
            {
                try { await conn.Channel.Writer.WriteAsync(message); }
                catch (ChannelClosedException) { /* connection closing */ }
            }
        }
    }

    public async Task BroadcastAsync(string eventType, object payload, string? id = null)
    {
        var data = JsonSerializer.Serialize(payload, _jsonOptions);
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

    private void RemoveConnection(string channel, Guid connectionId)
    {
        SseConnection? toClose = null;
        _connections.AddOrUpdate(channel,
            _ => [],
            (_, list) =>
            {
                toClose = list.FirstOrDefault(c => c.ConnectionId == connectionId);
                list.RemoveAll(c => c.ConnectionId == connectionId);
                return list;
            });

        toClose?.Channel.Writer.Complete();

        if (_connections.TryGetValue(channel, out var remaining) && remaining.Count == 0)
            _connections.TryRemove(channel, out _);

        if (_connections.Count == 0)
            StopKeepAlive();
    }

    private void StartKeepAlive()
    {
        _keepAliveCts = new CancellationTokenSource();
        _keepAliveTimer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        _ = KeepAliveLoopAsync(_keepAliveCts.Token);
    }

    private async Task KeepAliveLoopAsync(CancellationToken ct)
    {
        try
        {
            while (await _keepAliveTimer!.WaitForNextTickAsync(ct))
            {
                await BroadcastAsync(": keepalive", null!, null);
            }
        }
        catch (OperationCanceledException) { }
    }

    private void StopKeepAlive()
    {
        _keepAliveCts?.Cancel();
        _keepAliveTimer?.Dispose();
        _keepAliveCts?.Dispose();
    }

    private static async Task WriteMessageAsync(HttpResponse response, SseMessage message, CancellationToken ct)
    {
        if (message.EventType == ": keepalive")
        {
            var keepAliveBytes = Encoding.UTF8.GetBytes(": keepalive\n\n");
            await response.Body.WriteAsync(keepAliveBytes, ct);
            await response.Body.FlushAsync(ct);
            return;
        }

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
