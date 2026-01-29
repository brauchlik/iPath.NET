using Microsoft.Extensions.Options;
using Scalar.AspNetCore;
using System.IO;
using System.Text;

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
                    return Results.File(stream, contentType: "text/json", 
                        fileDownloadName: System.IO.Path.GetFileName(filename),
                        enableRangeProcessing: true);
                }
            }

            if (resource == "Questionnaire" && Guid.TryParse(id, out var guid))
            {
                var q = await mediator.Send(new GetQuestionnaireByIdQuery(guid), default);
                if (q != null)
                {
                    var byteArray = System.Text.Encoding.Default.GetBytes(q.Resource);
                    return Results.File(new MemoryStream(byteArray), contentType: "text/json", 
                        fileDownloadName: q.QuestionnaireId + ".json", 
                        enableRangeProcessing: true);
                }
            }

            return Results.NotFound();
        })
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        return route;
    }
}

