using iPath.Blazor.Componenents.ServiceRequests;

namespace iPath.Test.xUnit2.Documents;

public class ConversionStatusPollerTests
{
    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }
    }

    [Fact]
    public async Task Start_Polls_Until_Work_Is_Done()
    {
        var ticks = 0;
        var remaining = 2;
        using var poller = new ConversionStatusPoller(TimeSpan.FromMilliseconds(10));

        poller.Start(() => remaining > 0, () =>
        {
            ticks++;
            remaining--;
            return Task.CompletedTask;
        });

        await WaitUntilAsync(() => !poller.IsRunning, TimeSpan.FromSeconds(5));

        ticks.Should().Be(2);
        poller.IsRunning.Should().BeFalse();
    }

    [Fact]
    public async Task Start_While_Running_Does_Not_Start_A_Second_Loop()
    {
        var concurrency = 0;
        var maxConcurrency = 0;
        using var poller = new ConversionStatusPoller(TimeSpan.FromMilliseconds(10));

        async Task Tick()
        {
            var current = Interlocked.Increment(ref concurrency);
            maxConcurrency = Math.Max(maxConcurrency, current);
            await Task.Delay(5);
            Interlocked.Decrement(ref concurrency);
        }

        poller.Start(() => true, Tick);
        poller.Start(() => true, Tick);

        await Task.Delay(80);
        poller.Stop();
        await WaitUntilAsync(() => !poller.IsRunning, TimeSpan.FromSeconds(2));

        maxConcurrency.Should().Be(1);
        poller.IsRunning.Should().BeFalse();
    }

    [Fact]
    public async Task Stop_Stops_Ticking()
    {
        var ticks = 0;
        var poller = new ConversionStatusPoller(TimeSpan.FromMilliseconds(10));
        poller.Start(() => true, () => { Interlocked.Increment(ref ticks); return Task.CompletedTask; });

        await Task.Delay(50);
        poller.Stop();
        await WaitUntilAsync(() => !poller.IsRunning, TimeSpan.FromSeconds(2));

        var afterStop = ticks;
        await Task.Delay(50);

        ticks.Should().Be(afterStop);
        poller.Dispose();
    }

    [Fact]
    public async Task Start_After_Stop_Is_Possible()
    {
        var ticks = 0;
        using var poller = new ConversionStatusPoller(TimeSpan.FromMilliseconds(10));
        poller.Start(() => true, () => { Interlocked.Increment(ref ticks); return Task.CompletedTask; });
        await Task.Delay(30);
        poller.Stop();
        await WaitUntilAsync(() => !poller.IsRunning, TimeSpan.FromSeconds(2));

        var before = ticks;
        poller.Start(() => true, () => { Interlocked.Increment(ref ticks); return Task.CompletedTask; });
        await WaitUntilAsync(() => ticks > before, TimeSpan.FromSeconds(2));

        ticks.Should().BeGreaterThan(before);
    }
}
