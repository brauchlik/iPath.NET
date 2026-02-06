using Ardalis.GuardClauses;
using iPath.Application.Coding;
using iPath.Domain.Entities;
using iPath.EF.Core.Database;
using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace CodingAgent.Features.Coding;

public class CodingPlugin(CodingService coding, iPathDbContext db)
{
    private ServiceRequest request { get; set; }
    private ValueSetDisplay valueset;
    private List<CodedConcept>? _concepts;

    [KernelFunction("load_request")]
    [Description("Load a Request by its ID and get the description as text")]
    public async Task<string> LoadRequest(Guid id)
    {
        request = await db.ServiceRequests.FindAsync(id);
        Guard.Against.NotFound(id, request);

        if (valueset is null)
        {
            await coding.LoadCodeSystem();
            await coding.LoadValueSet("icdo-topo");
            valueset = coding.GetValueSetDisplay("icdo-topo");

            _concepts = valueset.ValueSet.Expansion.Contains.Select(x => new CodedConcept { Code = x.Code, Display = x.Display }).ToList();
        }
        return GetDescription();
    }



    //[KernelFunction("get_description")]
    //[Description("get the fulltext description of the request")]
    private string? GetDescription()
    {
        var text = "Title: " + request?.Description.Title + Environment.NewLine;
        if (!string.IsNullOrEmpty(request?.Description.Subtitle))
        {
            text += request?.Description.Subtitle + Environment.NewLine + Environment.NewLine;
        }
        text += request?.Description?.Text;
        return text;
    }


    [KernelFunction("get_bodysites")]
    [Description("Gets the list of valid ICD-O topography codes")]
    public IReadOnlyList<ICDO> GetBodySites()
    {
        Console.WriteLine("Plugin => get_bodysites");
        return _concepts.Select(x => new ICDO( Display: x.Display, Code: x.Code )).ToList();
    }


    [KernelFunction("set_bodysitecode")]
    [Description("Set the bodysite coding for the given Request and indicate the probability that the code matches the description. if uncertain, set --")]
    public void SetBodySiteCode(string? bodySiteCode, decimal probability)
    {
        Console.WriteLine("Body Site: {0}, probability = {1}", bodySiteCode, probability);
    }
}


public record ICDO(string Code, string Display);