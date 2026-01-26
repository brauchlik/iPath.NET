namespace iPath.Blazor.Componenents.ServiceRequests;

public abstract class ServiceRequestViewComponentBase : ComponentBase, IDisposable
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

    void IDisposable.Dispose()
    {
        vm.OnChange -= OnChangedHandler;
        OnDisposed();
    }

    protected virtual void OnDisposed() { }

    protected virtual void OnChangedHandler()
    {
        InvokeAsync(() => StateHasChanged());
    }
}
