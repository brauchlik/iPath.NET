using iPath.Blazor.Componenents.Nodes;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Logging;

namespace iPath.Blazor.Componenents.Nodes;

public partial class FileUploadButton(NodeViewModel vm, ISnackbar snackbar, ILogger<FileUploadButton> logger)
{
    [Parameter]
    public bool Disabled {  get; set; }

    [Parameter]
    public string Icon { get; set; } = Icons.Material.Filled.AttachFile;

    [Parameter]
    public int MaximumFileCount { get; set; } = 10;



    private bool uploading;
    int progress = 0;
    string progText = "";


    async Task StartUpload(InputFileChangeEventArgs e)
    {
        progress = 0;
        if (e.FileCount > 0)
        {
            uploading = true;
            try
            {
                int c = 0;
                foreach(var f in e.GetMultipleFiles())
                {
                    c++;
                    progress = (int)(100 * (double)c / (e.FileCount * 2));
                    progText = $"{c}/{e.FileCount}";
                    StateHasChanged();
                    await vm.UploadFile(f);
                    c++;
                    progress = (int)(100 * (double)c / (e.FileCount * 2));
                    StateHasChanged();
                }                
            }
            catch (Exception ex)
            {
                snackbar.Add(ex.Message, Severity.Warning);
            }
            finally
            {
                progText = "";
                uploading = false;                
            }
        }
    }
}