using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using iPath.Domain.Config;
using Microsoft.Extensions.Options;
using System.IO.Pipelines;
using System.Text.Json;

namespace iPath.Application.Fhir;

public class FileFhirDataLoader(IOptions<iPathConfig> opts) : IFhirDataLoader
{  

    public async Task<string> GetResourceAsync(string id, CancellationToken ct = default)
    {
        var filename = Path.Combine(opts.Value.FhirResourceFilePath, $"{id}.json");
        if (System.IO.File.Exists(filename))
        {
            return await System.IO.File.ReadAllTextAsync(filename, ct);
        }
        return string.Empty;
    }

    public async Task<T?> GetResourceAsync<T>(string id, CancellationToken ct = default) where T : Resource
    {
        var filename = Path.Combine(opts.Value.FhirResourceFilePath, $"{id}.json");
        if (System.IO.File.Exists(filename))
        {
            await using var fs = System.IO.File.OpenRead(filename);
            var pr = PipeReader.Create(fs, new StreamPipeReaderOptions(leaveOpen: false));

            var options = new JsonSerializerOptions().ForFhir(ModelInfo.ModelInspector);
            return await JsonSerializer.DeserializeAsync<T>(pr, options, ct);
        }
        return null;
    }
}
