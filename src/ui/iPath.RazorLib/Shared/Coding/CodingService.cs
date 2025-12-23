using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using System.Net.Http.Json;
using System.Text.Json;



namespace iPath.Razorlib.Coding;

public class CodingService(HttpClient http)
{
    private Hl7.Fhir.Model.CodeSystem topoCS = null;

    private async Task<bool> InitTopo()
    {
        if (topoCS == null)
        {
            try
            {
                var resp = await http.GetAsync("_content/iPath.Blazor.Componenents/icdo-topo.fhir");
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


    public async Task<IEnumerable<CodeSystem.ConceptDefinitionComponent>> GetTopoGroups(CancellationToken ct = default)
    {
        if (!await InitTopo())
            return new List<CodeSystem.ConceptDefinitionComponent>();
        // groups have no . in the code
        return topoCS.Concept.Where(x => !x.Code.Contains(".")).OrderBy(x => x.Display).ToArray();
    }

    public async Task<IEnumerable<CodeSystem.ConceptDefinitionComponent>> GetTopoCodeForGroup(string groupCode, CancellationToken ct = default)
    {
        if (!await InitTopo())
            return new List<CodeSystem.ConceptDefinitionComponent>();
        return topoCS.Concept.Where(x => x.Code.StartsWith(groupCode + ".")).OrderBy(x => x.Display).ToArray();
    }





    private Hl7.Fhir.Model.CodeSystem morphoCS = null;
    private async Task InitMorpho()
    {
        if (morphoCS == null)
        {
            try
            {
                var resp = await http.GetAsync("_content/iPath.RazorLib/icdo-morpho.fhir");
                var options = new JsonSerializerOptions().ForFhir(ModelInfo.ModelInspector);
                morphoCS = JsonSerializer.Deserialize<CodeSystem>(resp.Content.ReadAsStream(), options);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
    }

    public async Task<IEnumerable<CodeSystem.ConceptDefinitionComponent>> FindMorphoCodes(string search, CancellationToken ct = default)
    {
        await InitMorpho();
        search = search.ToLower();
        return morphoCS.Concept.Where(x => x.Display.ToLower().Contains(search)).ToArray();
    }
}