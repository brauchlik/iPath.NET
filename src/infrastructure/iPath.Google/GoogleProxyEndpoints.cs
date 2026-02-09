using Google.Apis.Auth.OAuth2;
using iPath.Domain.Config;
using iPath.Domain.Entities;
using iPath.EF.Core.Database;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;

namespace iPath.Google;

public static class GoogleProxyEndpoints
{
    public static IEndpointRouteBuilder MapGoogleProxy(this IEndpointRouteBuilder builder)
    {

        var grp = builder.MapGroup("google")
            .WithTags("Google");


        // proxy uwing local cache
        grp.MapGet("/proxy/{docId}", async (string docId, HttpContext context, iPathDbContext db, IHttpClientFactory clientFactory,
            IOptions<GoogleDriveConfig> driveOpts, IOptions<iPathConfig> ipathOpts) =>
        {
            var doc = await db.Documents.FindAsync(Guid.Parse(docId));
            if (doc == null || !doc.File.Storage.IsGoogle())
            {
                return Results.NotFound();
            }


            var localPath = Path.Combine(ipathOpts.Value.TempDataPath, doc.Id.ToString());

            // Download if not cached
            if (!File.Exists(localPath))
            {
                var storageId = doc.File.Storage.StorageId;

                // 1. Load Credentials and Get Token
                var json = await System.IO.File.ReadAllTextAsync(driveOpts.Value.ClientSecretPath);

                var credential = GoogleCredential.FromJson(json)
                    .CreateScoped("https://www.googleapis.com/auth/drive.readonly");

                var accessToken = await credential.UnderlyingCredential.GetAccessTokenForRequestAsync();

                var client = clientFactory.CreateClient();
                var requestUrl = $"https://www.googleapis.com/drive/v3/files/{storageId}?alt=media";
                var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                // Download the whole file
                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                if (!response.IsSuccessStatusCode) return Results.StatusCode((int)response.StatusCode);

                using var fs = new FileStream(localPath, FileMode.Create, FileAccess.Write, FileShare.None);
                await response.Content.CopyToAsync(fs);

            }

            // 3. Serve from Local Disk
            // 'enableRangeProcessing: true' is the magic that makes OpenSeadragon work!
            return Results.File(localPath, "image/tiff", enableRangeProcessing: true);

        })
            .RequireAuthorization()
            .RequireCors("CorsPolicy");


        // proxy with direct acces to google drive
        grp.MapGet("/proxy_direct/{docId}", async (string docId, HttpContext context, iPathDbContext db,
            IHttpClientFactory clientFactory, IOptions<GoogleDriveConfig> driveOpts) =>
        {
            var doc = await db.Documents.FindAsync(Guid.Parse(docId));
            if (doc == null || !doc.File.Storage.IsGoogle())
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                context.Response.ContentType = "text/plain";
                await context.Response.WriteAsync("Not Found");
                return;
            }

            var storageId = doc.File.Storage.StorageId;

            // 1. Load Credentials and Get Token
            var json = await System.IO.File.ReadAllTextAsync(driveOpts.Value.ClientSecretPath);

            var credential = GoogleCredential.FromJson(json)
                .CreateScoped("https://www.googleapis.com/auth/drive.readonly");

            var accessToken = await credential.UnderlyingCredential.GetAccessTokenForRequestAsync();

            // 2. Prepare the Request
            var client = clientFactory.CreateClient();
            var requestUrl = $"https://www.googleapis.com/drive/v3/files/{storageId}?alt=media";
            var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);

            // Use Bearer Token instead of API Key
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            // Forward the Range header for OpenSeadragon/GeoTIFF
            if (context.Request.Headers.TryGetValue("Range", out var range))
            {
                request.Headers.TryAddWithoutValidation("Range", range.ToString());
            }

            // 3. Execute and Stream
            var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

            context.Response.StatusCode = (int)response.StatusCode;

            // Map necessary headers back to the browser
            foreach (var header in response.Content.Headers)
            {
                context.Response.Headers[header.Key] = header.Value.ToArray();
            }

            context.Response.Headers["Access-Control-Allow-Origin"] = "*";
            context.Response.Headers["Accept-Ranges"] = "bytes";

            await response.Content.CopyToAsync(context.Response.Body);
        })
            .RequireAuthorization()
            .RequireCors("CorsPolicy");


        return builder;
    }
}
