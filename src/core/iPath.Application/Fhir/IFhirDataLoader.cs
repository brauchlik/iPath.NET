using Hl7.Fhir.Model;

namespace iPath.Application.Fhir;

public interface IFhirDataLoader
{
    Task<string> GetResourceAsync(string id, CancellationToken ct = default);
    Task<T?> GetResourceAsync<T>(string id, CancellationToken ct = default) where T : Resource;
    Task<IEnumerable<string>> ListAvailableValueSetIdsAsync(CancellationToken ct = default);
}
