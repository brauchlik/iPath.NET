using Microsoft.AspNetCore.Components.Server.Circuits;
using System.Collections.Concurrent;

namespace iPath.API.Services.Diagnostics;

/// <summary>
/// Logs the lifecycle of interactive server circuits. A circuit that closes after seconds, or a
/// connection that goes down while a user is browsing, is the signature of the app being restarted
/// (or the client losing the socket) - which leaves the browser showing the last rendered frame,
/// e.g. a page header stuck on "loading ..." until a reload builds a new circuit.
/// </summary>
public class CircuitDiagnostics(ILogger<CircuitDiagnostics> logger) : CircuitHandler
{
    private readonly ConcurrentDictionary<string, DateTime> _opened = new();

    public override Task OnCircuitOpenedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        _opened[circuit.Id] = DateTime.UtcNow;
        logger.LogInformation("Circuit {CircuitId} opened", circuit.Id);
        return Task.CompletedTask;
    }

    public override Task OnConnectionUpAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        logger.LogInformation("Circuit {CircuitId} connection up", circuit.Id);
        return Task.CompletedTask;
    }

    public override Task OnConnectionDownAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        logger.LogWarning("Circuit {CircuitId} connection down", circuit.Id);
        return Task.CompletedTask;
    }

    public override Task OnCircuitClosedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        var lifetime = _opened.TryRemove(circuit.Id, out var started)
            ? $"{(DateTime.UtcNow - started).TotalSeconds:F0} s"
            : "unknown";
        logger.LogInformation("Circuit {CircuitId} closed after {Lifetime}", circuit.Id, lifetime);
        return Task.CompletedTask;
    }
}
