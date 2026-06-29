using iPath.Application.Features.CaseRoom;
using iPath.EF.Core.Database;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Microsoft.Extensions.DependencyInjection;

namespace iPath.API.Middleware;

public class CaseRoomTokenAuthMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var token = context.Request.Query["token"].ToString();
        if (string.IsNullOrEmpty(token))
        {
            token = context.Request.Cookies["CaseRoomGuestToken"];
        }
        else
        {
            context.Response.Cookies.Append("CaseRoomGuestToken", token, new CookieOptions
            {
                HttpOnly = true,
                Secure = context.Request.IsHttps,
                SameSite = SameSiteMode.Strict,
                Path = "/"
            });
        }

        if (string.IsNullOrEmpty(token))
        {
            await next(context);
            return;
        }

        var path = context.Request.Path.Value ?? string.Empty;
        Guid? requestId = null;

        // Path /request/{id}/caseroom
        if (path.StartsWith("/request/", StringComparison.OrdinalIgnoreCase) && path.EndsWith("/caseroom", StringComparison.OrdinalIgnoreCase))
        {
            var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length >= 3 && Guid.TryParse(segments[1], out var id))
            {
                requestId = id;
            }
        }
        else if (path.StartsWith("/api/v1/requests/", StringComparison.OrdinalIgnoreCase))
        {
            var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length >= 4 && Guid.TryParse(segments[3], out var id))
            {
                requestId = id;
            }
        }
        else if (path.StartsWith("/api/v1/caseroom/", StringComparison.OrdinalIgnoreCase))
        {
            var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length >= 4 && Guid.TryParse(segments[3], out var id))
            {
                requestId = id;
            }
        }
        else if (path.StartsWith("/api/v1/documents/files/", StringComparison.OrdinalIgnoreCase))
        {
            var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length >= 5)
            {
                var docIdStr = segments[4];
                if (docIdStr.EndsWith(".dzi", StringComparison.OrdinalIgnoreCase))
                {
                    docIdStr = docIdStr[..^4];
                }
                else if (docIdStr.EndsWith("_files", StringComparison.OrdinalIgnoreCase))
                {
                    docIdStr = docIdStr[..^6];
                }

                if (Guid.TryParse(docIdStr, out var docId))
                {
                    var db = context.RequestServices.GetService<iPathDbContext>();
                    if (db != null)
                    {
                        requestId = await db.Documents
                            .Where(d => d.Id == docId)
                            .Select(d => d.ServiceRequestId)
                            .FirstOrDefaultAsync();
                    }
                }
            }
        }
        else if (path.StartsWith("/api/v1/events/stream", StringComparison.OrdinalIgnoreCase))
        {
            if (Guid.TryParse(context.Request.Query["requestId"].ToString(), out var id))
            {
                requestId = id;
            }
        }

        if (requestId.HasValue && requestId.Value != Guid.Empty)
        {
            var store = context.RequestServices.GetService<ICaseRoomSessionStore>();
            if (store != null)
            {
                if (await store.IsShareTokenValidAsync(requestId.Value, token, context.RequestAborted))
                {
                    var claims = new List<Claim>
                    {
                        new Claim(ClaimTypes.NameIdentifier, Guid.Empty.ToString()),
                        new Claim(ClaimTypes.Name, "Guest"),
                        new Claim(ClaimTypes.Role, "CaseRoomGuest"),
                        new Claim("AuthorizedRequestId", requestId.Value.ToString())
                    };

                    var identity = new ClaimsIdentity(claims, "CaseRoomGuestAuth");

                    if (context.User?.Identity?.IsAuthenticated == true)
                    {
                        context.User.AddIdentity(identity);
                    }
                    else
                    {
                        context.User = new ClaimsPrincipal(identity);
                    }
                }
            }
        }

        await next(context);
    }
}
