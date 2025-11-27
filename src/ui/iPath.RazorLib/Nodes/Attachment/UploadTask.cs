using iPath.Blazor.ServiceLib.ApiClient;
using Microsoft.AspNetCore.Components.Forms;
using Refit;

namespace iPath.Blazor.Componenents.Nodes;

public class UploadTask(IPathApi api)
{
    public bool uploading;
    public NodeDto? result;
    public string? Error;
    public string Filename = "";

    public Action OnChange;


    public void FromNode(NodeDto node)
    {
        result = node;
    }


    public async Task Upload(IBrowserFile file, Guid parentId)
    {
        uploading = true;
        Filename = file.Name;
        try
        {
            var s = new StreamPart(file.OpenReadStream(maxAllowedSize: IPathApi.MaxFileSize), file.Name, file.ContentType);
            var resp = await api.UploadNodeFile(s, parentId);
            if (resp.IsSuccessful)
            {
                result = resp.Content;
            }
            else 
            { 
                Error = resp.ErrorMessage;
            }
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
        uploading = false;

        OnChange?.Invoke();
    }

    public string ThumbUrl
        => $"data:image/jpeg;base64, {result.File.ThumbData}";

}