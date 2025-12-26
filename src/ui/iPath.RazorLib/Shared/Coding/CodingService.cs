using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using System.Text.Json;



namespace iPath.Razorlib.Coding;

public class CodingService(HttpClient http)
{
    const string CodingBaseUrl = "_content/iPath.Blazor.Componenents";

    private Hl7.Fhir.Model.CodeSystem topoCS = null;

    private async Task<bool> InitTopo()
    {
        if (topoCS == null)
        {
            try
            {
                var resp = await http.GetAsync($"{CodingBaseUrl}/icdo-topo.fhir");
                var options = new JsonSerializerOptions().ForFhir(ModelInfo.ModelInspector);
                topoCS = JsonSerializer.Deserialize<CodeSystem>(resp.Content.ReadAsStream(), options); 
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
        return topoCS != null;
    }

    public async Task<IEnumerable<CodeSystem.ConceptDefinitionComponent>> FindTopoCodes(string search, CancellationToken ct = default)
    {
        if (!await InitTopo())
            return new List<CodeSystem.ConceptDefinitionComponent>();

        search = search is null ? "" : search.ToLower();
        return topoCS.Concept.Where(x => x.Display.ToLower().Contains(search)).OrderBy(x => x.Display).ToArray();
    }

    public async Task<IEnumerable<CodedConcept>> FindTopoConcetps(string search, CancellationToken ct = default)
    {
        var codes = await FindTopoCodes(search, ct);
        return codes.Select(c => c.ToConcept()).ToArray();
    }


    public async Task<IEnumerable<CodeSystem.ConceptDefinitionComponent>> GetTopoGroups(CancellationToken ct = default)
    {
        if (!await InitTopo())
            return new List<CodeSystem.ConceptDefinitionComponent>();
        // groups have no . in the code
        return topoCS.Concept.Where(x => !x.Code.Contains(".")).OrderBy(x => x.Display).ToArray();
    }

    public async Task<IEnumerable<CodedConcept>> GetTopoGroupConcepts(CancellationToken ct = default)
    {
        if (!await InitTopo())
            return new List<CodedConcept>();
        // groups have no . in the code
        return topoCS.Concept.Where(x => !x.Code.Contains(".")).OrderBy(x => x.Display).Select(x => x.ToConcept()).ToArray();
    }



    public async Task<IEnumerable<CodeSystem.ConceptDefinitionComponent>> GetTopoCodeForGroup(string groupCode, CancellationToken ct = default)
    {
        if (!await InitTopo())
            return new List<CodeSystem.ConceptDefinitionComponent>();
        return topoCS.Concept.Where(x => x.Code.StartsWith(groupCode + ".")).OrderBy(x => x.Display).ToArray();
    }

    public async Task<IEnumerable<CodedConcept>> GetTopoConceptsForGroup(string groupCode, CancellationToken ct = default)
    {
        if (!await InitTopo())
            return new List<CodedConcept>();
        return topoCS.Concept.Where(x => x.Code.StartsWith(groupCode + ".")).OrderBy(x => x.Display).Select(x => x.ToConcept()).ToArray();
    }





    private Hl7.Fhir.Model.CodeSystem morphoCS = null;
    private async Task<bool> InitMorpho()
    {
        if (morphoCS == null)
        {
            try
            {
                var resp = await http.GetAsync($"{CodingBaseUrl}/icdo-morpho.fhir");
                var options = new JsonSerializerOptions().ForFhir(ModelInfo.ModelInspector);
                morphoCS = JsonSerializer.Deserialize<CodeSystem>(resp.Content.ReadAsStream(), options);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
        return morphoCS != null;
    }

    public async Task<IEnumerable<CodeSystem.ConceptDefinitionComponent>> FindMorphoCodes(string search, CancellationToken ct = default)
    {
        if (!await InitMorpho())
            return new List<CodeSystem.ConceptDefinitionComponent>();
        search = search.ToLower();
        return morphoCS.Concept.Where(x => x.Display.ToLower().Contains(search)).ToArray();
    }


    public async Task<IEnumerable<CodedConcept>> FindMorphoConcepts(string search, CancellationToken ct = default)
    {
        if (!await InitMorpho())
            return new List<CodedConcept>();
        search = search.ToLower();
        return morphoCS.Concept.Where(x => x.Display.ToLower().Contains(search)).Select(x => x.ToConcept()).ToArray();
    }
}
