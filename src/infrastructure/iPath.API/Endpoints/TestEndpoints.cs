using Ardalis.GuardClauses;
using iPath.EF.Core.Database;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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



        test.MapGet("stats/documents", async (iPathDbContext db, CancellationToken ct) =>
        {
            return await db.Documents.AsNoTracking()
                .GroupBy(x => x.DocumentType)
                .Select(g => new { Type = g.Key, Count = g.Count() })
                .OrderByDescending(g => g.Count)
                .ToListAsync();
        });

        return route;
    }
}


public class MyUpload
{
    public string Filename { get; set; }
    public string Mimetype { get; set; }
    public byte[]? Content { get; set; }
}