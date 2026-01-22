using Microsoft.Extensions.Options;
using Scalar.AspNetCore;

namespace iPath.API;

public static class FhirEndpoints
{
    public static IEndpointRouteBuilder MapFhirApi(this IEndpointRouteBuilder route)
    {
        var fhir = route.MapGroup("fhir")
                .WithTags("Fhir");

        fhir.MapGet("{resource}/{id}", async (string resource, string id, IOptions<iPathConfig> opts) =>
        {
            var dir = opts.Value.FhirResourceFilePath;
            if (System.IO.Directory.Exists(dir))
            {
                var filename = System.IO.Path.Combine(dir, resource);
                filename = System.IO.Path.Combine(filename, id) + ".json";
                if (System.IO.File.Exists(filename))
                {
                    using var stream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read);
                    return Results.File(stream, contentType: "text/json");
                }
            }

            return Results.NotFound();
        })
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        return route;
    }
}

