using System.ComponentModel;

namespace iPath.Blazor.Componenents.Documents;

public partial class GalleryDropZone
{
    [Parameter, DefaultValue(25)]
    public int MaximumFileCount { get; set; } = 25;

    private const string DefaultDragClass = "relative rounded-lg border-2 border-dashed pa-2 mt-2 mud-width-full mud-height-full";
    private string _dragClass = DefaultDragClass;
    private void SetDragClass()
        => _dragClass = $"{DefaultDragClass} mud-border-primary mud-background-gray";

    private void ClearDragClass()
        => _dragClass = DefaultDragClass;


    private List<UploadTask> _uploads = new();

    Action OnChanged;


    protected override void OnInitialized()
    {
        base.OnInitialized();
        vm.OnUploadStarted += OnUploadStartedHandler;
        if (vm is not null && vm.SelectedDocument is not null)
        {
            foreach (var c in vm.SelectedRequest.Documents.Where(n => n.ParentNodeId == vm.SelectedDocument.Id).OrderBy(n => n.SortNr).ToList())
            {
                var t = vm.CreateUploadTask();
                t.FromNode(c);
                _uploads.Add(t);
            }
        }
    }

    protected override void OnDisposed()
    {
        vm.OnUploadStarted -= OnUploadStartedHandler;
        base.OnDisposed();
    }


    private async Task OnInputFileChanged(InputFileChangeEventArgs e)
    {
        foreach (var f in e.GetMultipleFiles(MaximumFileCount))
        {
            var t = vm.CreateUploadTask();
            await t.Upload(f, vm.SelectedRequest.Id, vm.SelectedDocument?.Id).ConfigureAwait(false);
            _uploads.Add(t);
            await InvokeAsync(StateHasChanged);
        }
    }


    private void OnUploadStartedHandler(UploadTask t)
    {
        _uploads.Add(t);
        InvokeAsync(StateHasChanged);
    }
}