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
    [Description("Load a Request by its ID")]
    public async Task LoadRequest(Guid id)
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
    }



    [KernelFunction("get_description")]
    [Description("Gets the fulltext description of the request")]
    public string? GetDescription()
    {
        return request?.Description?.Text;
    }


    [KernelFunction("get_bodysites")]
    [Description("Gets a list icd-o concepts to code a bodysite")]
    public List<CodedConcept> GetBodySites()
    {
        return _concepts;
    }


    [KernelFunction("set_bodysitecode")]
    [Description("Set the bodysite coding for the given Request and indicate the probability that the code matches the description. leave code null if uncertain")]
    public void SetBodySiteCode(string? bodySiteCode, decimal probability)
    {
        Console.WriteLine("Body Site: {0}, probability = {1}", bodySiteCode, probability);
    }
}
