using Microsoft.JSInterop;

namespace iPath.OpenSeadragon;

public class OsdInterop
{
    private readonly Lazy<Task<IJSObjectReference>> moduleTask;

    public OsdInterop(IJSRuntime jsRuntime)
    {
        moduleTask = new(() => jsRuntime.InvokeAsync<IJSObjectReference>(
            "import", "./_content/iPath.OpenSeadragon/osd.interop.js").AsTask());
    }

    public async ValueTask DisposeAsync()
    {
        if (moduleTask.IsValueCreated)
        {
            var module = await moduleTask.Value;
            await module.DisposeAsync();
        }
    }

    public async Task LoadJS()
    {
        var module = await moduleTask.Value;
        await module.InvokeAsync<object>("initSVS");
    }
}
