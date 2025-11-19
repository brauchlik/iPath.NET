using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using System.Text;
using System.Text.Json;

namespace iPath.DataImport;

public static class CodingImport
{

    public static void ImportCodes()
    {
        if (System.IO.File.Exists("icdo-topo.csv"))
        {
            ImportCsvToFhieCodeSystem("icdo-topo.csv", "icdo-topo.fhir");
        }
        if (System.IO.File.Exists("icdo-morpho.csv"))
        {
            ImportCsvToFhieCodeSystem("icdo-morpho.csv", "icdo-morpho.fhir");
        }
    }

    public static void ImportCsvToFhieCodeSystem(string infile, string outfile)
    {
        Console.WriteLine("importing " + infile + " ....");

        if (System.IO.File.Exists(outfile))
            System.IO.File.Delete(outfile);

        var cs = new CodeSystem
        {
            Url = "http://terminology.hl7.org/CodeSystem/icd-o-3",
            Identifier = new List<Identifier>()
                {
                    new Identifier {System = "urn:ietf:rfc:3986", Value = "urn:oid:2.16.840.1.113883.6.43.1"}
                },
            Name = "IcdO3",
            Title = "International Classification of Diseases for Oncology, version 3",
            Status = PublicationStatus.Active,
            Experimental = false,
            Description = "International Classification of Diseases for Oncology, version 3. For more information see http://www.who.int/classifications/icd/adaptations/oncology/en/.",
            CaseSensitive = true,
            Content = CodeSystemContentMode.NotPresent
        }
    ;

        cs.Concept = new();

        using (var fileStream = File.OpenRead(infile))
        using (var streamReader = new StreamReader(fileStream, Encoding.UTF8, true))
        {
            String line;
            while ((line = streamReader.ReadLine()) != null)
            {
                string[] words = line.Split(';');
                if (words.Length == 2)
                {
                    cs.Concept.Add(new CodeSystem.ConceptDefinitionComponent
                    {
                        Code = words[0],
                        Display = words[1]
                    });
                }
            }
        }

        var options = new JsonSerializerOptions().ForFhir(ModelInfo.ModelInspector).Pretty();
        string csJson = JsonSerializer.Serialize(cs, options);

        // validate
        var optionsRead = new JsonSerializerOptions().ForFhir(ModelInfo.ModelInspector);
        var test = JsonSerializer.Deserialize<CodeSystem>(csJson, optionsRead);

        // write to file
        System.IO.File.WriteAllText(outfile, csJson);

        Console.WriteLine("written to " + outfile);
    }
}