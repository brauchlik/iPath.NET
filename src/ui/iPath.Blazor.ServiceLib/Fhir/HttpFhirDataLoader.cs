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
    private readonly string _baseAddress;

    public HttpFhirDataLoader(IConfiguration config, ILogger<HttpFhirDataLoader> logger)
    {
        _logger = logger;
        _baseAddress = config["BaseAddress"] + "api/v1/fhir/";
    }
    

    public async Task<string> GetResourceAsync(string id, CancellationToken ct = default)
    {
        try
        {
            var http = new HttpClient { BaseAddress = new Uri(_baseAddress) };
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
            var http = new HttpClient { BaseAddress = new Uri(_baseAddress) };
            _logger.LogInformation("Getting Fhir Resource {0} {1}", _baseAddress, id);
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
