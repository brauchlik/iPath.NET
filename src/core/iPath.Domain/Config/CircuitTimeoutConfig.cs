namespace iPath.Domain.Config;

/// <summary>
/// Blazor circuit timeouts. The framework defaults are hostile to debugging and to slow clients:
/// ClientTimeoutInterval is 30 s (the connection is dropped when a client - or a paused debugger -
/// misses its keep-alives) and JSInteropDefaultCallTimeout is 60 s (a pending NavigationManager
/// call is cancelled after that and, because the navigation runs as an async void, the resulting
/// exception cannot be caught anywhere and takes the whole circuit down with it - the browser then
/// stays on its last rendered frame). Values are in seconds; zero or less keeps the framework default.
/// </summary>
public class CircuitTimeoutConfig
{
    public const string ConfigName = "CircuitTimeouts";

    public int ClientTimeoutSeconds { get; set; } = 300;
    public int JSInteropCallTimeoutSeconds { get; set; } = 300;
    public int DisconnectedCircuitRetentionSeconds { get; set; } = 600;
}
