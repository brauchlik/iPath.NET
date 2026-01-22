using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using iPath.Application.Fhir;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.IO.Pipelines;
using System.Text.Json;

namespace iPath.Blazor.ServiceLib.Fhir;

public class HttpFhirDataLoader : IFhirDataLoader
{
    private readonly ILogger<HttpFhirDataLoader> _logger;
    private readonly IHttpClientFactory _fct;

    public HttpFhirDataLoader(IHttpClientFactory fct, ILogger<HttpFhirDataLoader> logger)
    {
        _logger = logger;
        _fct = fct;
    }
    

    public async Task<string> GetResourceAsync(string id, CancellationToken ct = default)
    {
        try
        {
            using var http = _fct.CreateClient("Fhir");
            _logger.LogInformation("Getting Fhir Resource {0} {1}", http.BaseAddress, id);
            var resp = await http.GetAsync(id);
            if (resp.IsSuccessStatusCode)
            {
                return await resp.Content.ReadAsStringAsync();
            }
            else
            {
                _logger.LogError("FHIR Resource {0} not found", id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.Message);
        }
        return string.Empty;
    }

    public async Task<T?> GetResourceAsync<T>(string id, CancellationToken ct = default) where T : Resource
    {
        try
        {
            using var http = _fct.CreateClient("Fhir");
            _logger.LogInformation("Getting Fhir Resource {0} {1}", http.BaseAddress, id);
            var resp = await http.GetAsync(id);
            if (resp.IsSuccessStatusCode)
            {
                var pr = PipeReader.Create(resp.Content.ReadAsStream(), new StreamPipeReaderOptions(leaveOpen: false));

                var options = new JsonSerializerOptions().ForFhir(ModelInfo.ModelInspector);
                return await JsonSerializer.DeserializeAsync<T>(pr, options, ct);
            }
            else
            {
                _logger.LogError("FHIR Resource {0} not found", id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.Message);
        }
        return null;
    }
}
