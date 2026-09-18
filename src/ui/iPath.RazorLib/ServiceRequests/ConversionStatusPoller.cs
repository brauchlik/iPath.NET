namespace iPath.Blazor.Componenents.ServiceRequests;

/// <summary>
/// Drives a fixed-interval background loop that reloads a view while <paramref name="hasWork"/>
/// still reports work. Used to refresh document conversion status after an async server upload
/// so the gallery/viewer updates itself instead of waiting for a manual reload.
///
/// One loop per instance: <see cref="Start"/> is a no-op while a loop is already running, so a
/// burst of parallel uploads all funnel into the same poller.
/// </summary>
public sealed class ConversionStatusPoller(TimeSpan interval) : IDisposable
{
    private CancellationTokenSource? _cts;

    public bool IsRunning => _cts is not null;

    public void Start(Func<bool> hasWork, Func<Task> tick)
    {
        if (_cts is not null) return;

        var cts = new CancellationTokenSource();
        _cts = cts;
        _ = RunAsync(hasWork, tick, cts);
    }

    // Clears the running flag synchronously so a Start immediately after Stop (e.g. loading
    // another case) reliably begins a new loop; the old loop still observes the cancellation and
    // disposes its own token source.
    public void Stop()
    {
        var cts = _cts;
        _cts = null;
        cts?.Cancel();
    }

    private async Task RunAsync(Func<bool> hasWork, Func<Task> tick, CancellationTokenSource cts)
    {
        var ct = cts.Token;
        try
        {
            while (!ct.IsCancellationRequested && hasWork())
            {
                await Task.Delay(interval, ct);
                if (ct.IsCancellationRequested) break;

                try
                {
                    await tick();
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    break;
                }
                catch
                {
                    // A transient reload failure must not permanently stop status updates; the
                    // next tick retries. ReloadNode already logs the underlying error.
                }
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            if (ReferenceEquals(_cts, cts)) _cts = null;
            cts.Dispose();
        }
    }

    // The loop owns disposal of its own CancellationTokenSource (RunAsync's finally), so Dispose
    // only signals cancellation; disposing here would race the still-running loop.
    public void Dispose() => Stop();
}
