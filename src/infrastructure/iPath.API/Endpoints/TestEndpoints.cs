using Ardalis.GuardClauses;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Scalar.AspNetCore;

namespace iPath.API;

public static class TestEndpoints
{
    public static IEndpointRouteBuilder MapTestApi(this IEndpointRouteBuilder route)
    {
        var test = route.MapGroup("test")
                .WithTags("Test");

        test.MapPost("notify", async (TestEvent evt, [FromServices] IMediator mediator, CancellationToken ct)
                => await mediator.Publish(evt, ct))
                .RequireAuthorization();


        test.MapPost("upload", async (IFormFile file, int id = 2, [FromServices] IOptions<iPathConfig> opts = null) =>
        {
            if (!System.IO.Directory.Exists(opts.Value.TempDataPath))
                return Results.BadRequest();

            if (file.Length > 0)
            {
                var filePath = Path.Combine(opts.Value.TempDataPath, $"{id}-{file.FileName}");

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                return Results.Ok(new { FilePath = filePath });
            }
            return Results.BadRequest("Invalid file.");
        })
            .DisableAntiforgery();


        return route;
    }
}


public class MyUpload
{
    public string Filename { get; set; }
    public string Mimetype { get; set; }
    public byte[]? Content { get; set; }
}