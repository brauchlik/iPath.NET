using iPath.Domain.Config;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Options;
using Refit;

namespace iPath.Blazor.Componenents.Nodes;

public class UploadTask(IPathApi api, long MaxFileSize)
{
    public bool uploading { get; private set; }
    public NodeDto? Result { get; private set; }
    public string? Error { get; private set; }
    public bool IsSuccessful {  get; private set; }
    public string Filename { get; private set; } = "";

    public Action OnChange;

    public void FromNode(NodeDto node)
    {
        Result = node;
    }


    public async Task Upload(IBrowserFile file, Guid parentId)
    {
        uploading = true;
        Filename = file.Name;
        try
        {
            var s = new StreamPart(file.OpenReadStream(maxAllowedSize: MaxFileSize), file.Name, file.ContentType);
            var resp = await api.UploadNodeFile(s, parentId);
            if (resp.IsSuccessful)
            {
                IsSuccessful = true;
                Result = resp.Content;
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
        => $"data:image/jpeg;base64, {Result.File.ThumbData}";

}