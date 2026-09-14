using Refit;

namespace iPath.Blazor.Componenents.Extensions;

public static class ApiResponseExtensions
{
    extension(IApiResponse resp)
    {   
        public string ErrorMessage =>
         resp.HasResponseError(out var apiEx) && !string.IsNullOrEmpty(apiEx.Content) ? apiEx.Content :
         resp.Error?.InnerException?.Message ??
         resp.Error?.Message ??
         resp.ReasonPhrase ??
         string.Empty;
    }
}
