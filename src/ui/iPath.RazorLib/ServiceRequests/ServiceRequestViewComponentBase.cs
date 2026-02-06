
namespace iPath.Blazor.Componenents.ServiceRequests;

public abstract class ServiceRequestViewComponentBase : ComponentBase, IAsyncDisposable
{
    [Inject] 
    protected ServiceRequestViewModel vm { get; set; }

    [Inject]
    protected ISnackbar snackbar { get; set; }

    [Inject]
    protected IDialogService dialogService { get; set; }


    protected override void OnInitialized()
    {
        vm.OnChange += OnChangedHandler;
    }

    public async ValueTask DisposeAsync()
    {
        vm.OnChange -= OnChangedHandler;
        await OnDisposedAsync();
    }

    protected virtual ValueTask OnDisposedAsync() => ValueTask.CompletedTask;

    protected virtual void OnChangedHandler()
    {
        InvokeAsync(() => StateHasChanged());
    }
}
