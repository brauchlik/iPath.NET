using Microsoft.AspNetCore.Http;

namespace iPath.Blazor.Server;

public class ForwardCookiesHandler : baseAuthDelegationHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ForwardCookiesHandler(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var ctx = _httpContextAccessor.HttpContext;
        if (ctx != null)
        {
            // Forward Cookie header (for cookie auth)
            if (ctx.Request.Headers.TryGetValue("Cookie", out var cookie) && !string.IsNullOrEmpty(cookie))
            {
                if (!request.Headers.Contains("Cookie"))
                    request.Headers.TryAddWithoutValidation("Cookie", (string)cookie);
            }

            // Forward Authorization header (e.g., Bearer token)
            if (ctx.Request.Headers.TryGetValue("Authorization", out var auth) && !string.IsNullOrEmpty(auth))
            {
                if (!request.Headers.Contains("Authorization"))
                    request.Headers.TryAddWithoutValidation("Authorization", (string)auth);
            }
        }

        return base.SendAsync(request, cancellationToken);
    }
}