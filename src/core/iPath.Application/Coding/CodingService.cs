using Hl7.Fhir.Model;
using iPath.Application.Coding;
using iPath.Application.Fhir;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace iPath.Application.Coding;

public class CodingService
{
    private readonly ILogger<CodingService> logger;
    private readonly IFhirDataLoader loader;
    private readonly Dictionary<string, ValueSetDisplay> _valueSet = new();
    private readonly string _codeSystemId;
    private CodeSystem _codeSystem;
    private CodeSystemLookup? _lookup;



    public CodingService(IServiceProvider sp, string csKey)
    {
        logger = sp.GetRequiredService<ILogger<CodingService>>();
        loader = sp.GetRequiredService<IFhirDataLoader>();
        var config = sp.GetRequiredService<IConfiguration>();
        _codeSystemId = config[$"CodeSystems:{csKey}"] ?? csKey;
    }

    public CodingService(ILogger<CodingService> logger, IFhirDataLoader loader, string codeSystemId)
    {
        this.logger = logger;
        this.loader = loader;
        _codeSystemId = codeSystemId;
    }


    public async Task LoadCodeSystem()
    {
        if (_codeSystem is null && !string.IsNullOrEmpty(_codeSystemId))
        {
            _codeSystem = await loader.GetResourceAsync<CodeSystem>($"CodeSystem/{_codeSystemId}");
            _lookup = new CodeSystemLookup(_codeSystem);
        }
    }

    public CodeSystem CodeSystem => _codeSystem;
    public string CodeSystemUrl => _codeSystem is null ? "" : _codeSystem.Url.ToLowerInvariant().Trim();

    public async Task LoadValueSet(string id)
    {
        var vs = await loader.GetResourceAsync<ValueSet>($"ValueSet/{id}");
        if (vs is not null)
            LoadValueSet(vs, id);
    }


    public async Task LoadValueSet(ValueSet vs, string? vsId = null)
    {
        vsId ??= vs.Id;

        if (string.IsNullOrEmpty(vsId)) throw new ArgumentNullException("ValueSet.Id");
        vsId = vsId.ToLowerInvariant().Trim();

        // assert hat valueset beldongs to a code system
        var vsSystemUrl = vs.Compose?.Include.FirstOrDefault()?.System.ToLowerInvariant().Trim();
        if (string.IsNullOrEmpty(vsSystemUrl)) throw new ArgumentNullException("ValueSet.Compose.Include.System");

        // asser that coodesystem has been loaded
        if (CodeSystemUrl != vsSystemUrl) throw new ArgumentNullException("CodeSystem not loaded");

        var provider = new ValueSetDisplay(CodeSystem, vs);

        if (_valueSet.ContainsKey(vsId))
        {
            _valueSet[vsId] = provider;
        }
        else
        {
            _valueSet.Add(vsId, provider);
        }
    }


    public ValueSetDisplay? GetValueSetDisplay(string vsId)
    {
        if (!string.IsNullOrEmpty(vsId))
        {
            vsId = vsId.ToLowerInvariant().Trim();
            if (_valueSet.ContainsKey(vsId))
            {
                return _valueSet[vsId];
            }
        }
        return null;
    }




    public CodeLookupResult GetChildCodes(string root, bool includeRoot = true)
    {
        if (_lookup == null)
            return CodeLookupResult.WithError("no codesystem loaded");

        return _lookup.GetChildCodes(root, includeRoot);
    }

    public CodeLookupResult GetChildCodes(string root, string valueSetId, bool includeRoot = true)
    {
        if (_lookup == null)
            return CodeLookupResult.WithError("no codesystem loaded");

        if (string.IsNullOrWhiteSpace(valueSetId))
            return CodeLookupResult.WithError("valueset id required");

        valueSetId = valueSetId.ToLowerInvariant().Trim();
        if (!_valueSet.ContainsKey(valueSetId))
            return CodeLookupResult.WithError($"valueset '{valueSetId}' not loaded");

        var provider = _valueSet[valueSetId];

        var result = _lookup.GetChildCodes(root, includeRoot);
        if (!result.IsSuccess)
            return result;

        // Filter out codes that are not marked as included in the ValueSet
        var filtered = result.ChildCodes
            .Where(code =>
            {
                var dsp = provider.GetByCode(code);
                return dsp is not null && dsp.InValueSet;
            })
            .ToHashSet();

        return new CodeLookupResult(result.RootCode, filtered, result.Elapsed);
    }


    public bool IsChildCode(string childCode, string rootCode, bool includeRoot = true)
    {
        if (_lookup == null)
            return false;

        return _lookup.IsChildCode(childCode, rootCode, includeRoot);
    }



    public bool InConceptFilter(string code, ConceptFilter filter, bool includeRoot = true)
    {
        if (_lookup == null)
            return false;

        // no filter/concepts => true
        if (filter is null || filter.Concetps.IsEmpty())
            return true;

        foreach(var concept in filter.Concetps)
        {
            if (_lookup.IsChildCode(code, concept.Code, includeRoot))
            {
                return true; 
            }
        }

        return false;
    }

}