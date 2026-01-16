using Refit;

namespace iPath.Blazor.Componenents.Extensions;

public static class ApiResponseExtensions
{
    extension (IApiResponse resp) {
        public string ErrorMessage => resp.Error?.Content ?? resp.Error?.Message ?? string.Empty;
    }
}
