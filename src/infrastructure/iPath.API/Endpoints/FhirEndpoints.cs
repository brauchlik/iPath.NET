using Microsoft.Extensions.Options;
using Scalar.AspNetCore;
using System.IO;

namespace iPath.API;

public static class FhirEndpoints
{
    public static IEndpointRouteBuilder MapFhirApi(this IEndpointRouteBuilder route)
    {
        var fhir = route.MapGroup("fhir")
                .WithTags("Fhir");



        fhir.MapGet("{resource}/{id}", async (string resource, string id, IOptions<iPathConfig> opts, IMediator mediator) =>
        {
            var dir = opts.Value.FhirResourceFilePath;
            if (System.IO.Directory.Exists(dir))
            {
                var filename = System.IO.Path.Combine(dir, resource);
                filename = System.IO.Path.Combine(filename, id) + ".json";
                if (System.IO.File.Exists(filename))
                {
                    var stream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read);
                    return Results.File(stream, contentType: "application/octet-stream");
                }
            }

            if (resource == "Questionnaire" && Guid.TryParse(id, out var guid))
            {
                var q = await mediator.Send(new GetQuestionnaireByIdQuery(guid), default);
                if (q != null)
                {
                    return Results.File(q.Resource, contentType: "application/octet-stream", fileDownloadName: q.QuestionnaireId + ".json");
                }
            }

            return Results.NotFound();
        })
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        return route;
    }
}

