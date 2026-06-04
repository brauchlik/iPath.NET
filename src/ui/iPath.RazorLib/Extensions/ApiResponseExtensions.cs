using Refit;

namespace iPath.Blazor.Componenents.Extensions;

public static class ApiResponseExtensions
{
    extension(IApiResponse resp)
    {   
        public string ErrorMessage =>
         !string.IsNullOrEmpty(resp.Error?.Content) ? resp.Error.Content :
         resp.Error?.InnerException?.Message ??
         resp.Error?.Message ??
         resp.ReasonPhrase ??
         string.Empty;
    }
}
