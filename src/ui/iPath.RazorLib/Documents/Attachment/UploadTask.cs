using iPath.Application.Features.Documents;
using Refit;

namespace iPath.Blazor.Componenents.Documents;

public class UploadTask(IPathApi api, long MaxFileSize)
{
    public bool uploading { get; private set; }
    public DocumentDto? Result { get; private set; }
    public string? Error { get; private set; }
    public bool IsSuccessful {  get; private set; }
    public string Filename { get; private set; } = "";

    public Action OnChange;

    public void FromNode(DocumentDto node)
    {
        Result = node;
    }


    public async Task Upload(IBrowserFile file, Guid requestId, Guid? parentId)
    {
        uploading = true;
        Filename = file.Name;
        try
        {
            if (file.Size > MaxFileSize)
            {
                Error = $"File '{file.Name}' exceeds the maximum allowed size of {MaxFileSize} bytes.";
                uploading = false;
                OnChange?.Invoke();
                return;
            }

            var s = new StreamPart(file.OpenReadStream(maxAllowedSize: MaxFileSize), file.Name, file.ContentType);
            var resp = await api.UploadDocument(s, requestId, parentId);
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