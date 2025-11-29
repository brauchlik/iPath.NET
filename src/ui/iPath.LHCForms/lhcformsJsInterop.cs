using Microsoft.JSInterop;

namespace iPath.LHCForms;

public class lhcformsJsInterop : IAsyncDisposable
{
    private readonly Lazy<Task<IJSObjectReference>> moduleTask;

    public lhcformsJsInterop(IJSRuntime jsRuntime)
    {
        moduleTask = new(() => jsRuntime.InvokeAsync<IJSObjectReference>(
            "import", "./_content/iPath.LHCForms/lhcformsJsInterop.js").AsTask());
    }

    public async ValueTask<string> GetDataAsync(string componentId)
    {
        var module = await moduleTask.Value;
        return await module.InvokeAsync<string>("getData", componentId, options);
    }

    public async ValueTask LoadDataAsync(string questionnaireJson, string responseJson, string componentId)
    {
        var module = await moduleTask.Value;
        await module.InvokeVoidAsync("loadData", questionnaireJson, responseJson, componentId, options);
    }

    public async ValueTask DisposeAsync()
    {
        if (moduleTask.IsValueCreated)
        {
            var module = await moduleTask.Value;
            await module.DisposeAsync();
        }
    }

    private static string? options = null!;

    private static string? options_x =
        """
        {
            "fhirVersion": "R4",
            "prepopulate": false
        }
        """;
}