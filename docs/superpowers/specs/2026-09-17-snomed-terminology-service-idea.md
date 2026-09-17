# SNOMED CT terminology service in C# — idea capture

> **Status: idea, not approved, not implemented.** Captured 2026-09-17 so it is not lost.
> Deliberately *not* coded. When we pick this up we run the full brainstorming → spec →
> implementation-plan sequence; this file is the parking place, not the design.

## Why this came up

While running the questionnaire SNOMED pass, the `snomed-ct` MCP server (a local Python
process) could not be used: Snowstorm's search index on `basyssrvdock1:8082` is degraded
(search endpoints hang; only by-code `$lookup` answered, ~21 s). Two things became clear:

1. **The MCP is thin.** It wraps one search endpoint plus a couple of traversals. It
   exposes no concept attributes (semantic tag, active/retired, relationships, edition),
   no validation, and no ECL. Its local-HTTP timeouts (10 s search / 30 s expand) are also
   too tight for our server even when healthy.
2. **We will need this capability in C# anyway.** Other C# applications need an LLM (and
   application code) to query Snowstorm correctly — with the right attributes and
   validation — and ECL evaluation is likely to be needed later.

So: build the capability once, in C#, and expose it through several fronts.

## Intended shape (three consumers, one core)

```
                 +----------------------------------------------+
                 |  Core library (C#)                           |
                 |  SNOMED CT client over Snowstorm/FHIR R4     |
                 |  term search, concept by code, attributes,   |
                 |  validation, hierarchy, (later) ECL          |
                 +----------------------------------------------+
                    |                  |                    |
        in-process  |                  |                    |
                    v                  v                    v
        iPath / other C# apps   stdio MCP server      Microsoft.Extensions.AI
        (DI service)            (code agents)         tool (AIFunction)
```

One core, three adapters. The adapters must stay thin so behaviour cannot diverge.

## Consumers

| # | Consumer | What it needs |
|---|---|---|
| 1 | C# applications (iPath and others) | A DI service: search, resolve, validate, traverse. Deterministic, testable, no LLM in the loop. |
| 2 | Code agents (this environment) | A standalone **stdio MCP** exposing the same operations as tools, so agents can look codes up without ad-hoc HTTP scripting. |
| 3 | MS.Extensions.AI hosts | The same operations as an `AIFunction` tool so an LLM in a C# app can call a validated lookup instead of inventing codes. |

## Functional requirements (draft)

- **Term search** — return candidates with the attributes that matter: concept id, PT,
  FSN, **semantic tag**, active/retired, synonyms, edition/module. Current MCP returns
  code + display only, which is exactly why codes can be picked wrongly.
- **Concept by code** — full detail: descriptions, parents/ancestors, children.
- **Validation** — is this code valid in *our* edition? active? does its semantic tag
  match the expected type (finding vs procedure vs observable vs body structure)?
- **Hierarchy** — descendants / ancestors / subsumption tests (is-a), reusing
  `CodeSystemLookup`'s existing parent/child closure.
- **ECL (later)** — evaluate ECL server-side first; only implement locally if needed.
- **Edition awareness** — never silently mix editions. The Australian-edition incident
  (see below) is the cautionary example.
- **Backends** — our Snowstorm first; a hosted FHIR server as fallback (already how the
  Python MCP works, `SNOMED_BACKEND=remote`).
- **Caching** — by-code lookups and search results; our healthy-server latency was 1–20 s,
  so caching is not optional for interactive use.
- **Honest failure** — a degraded server must produce a clear error, never a plausible
  but unverified code. No fabricated SCTIDs, ever.

## Existing code to reuse (verified in this repo)

| Asset | Path | Reuse |
|---|---|---|
| `CodeSystemLookup` | `src/core/iPath.Application/Coding/CodeSystemLookup.cs` | already builds parent→child and child→parent closure with visited-set traversal; the is-a / subsumption core |
| `CodingService` | `src/core/iPath.Application/Coding/CodingService.cs` | loads and caches a `CodeSystem`, wraps lookups |
| `ValueSetTranslationService`, `CodeSystemTranslationService`, `CodingExtensions`, `ValueSetDisplay`, `CodeDisplay`, `CodeLookupResult` | `.../Coding/` | existing coding abstractions and result types |
| `IFhirDataLoader`, `FileFhirDataLoader`, `HttpFhirDataLoader` | `src/core/iPath.Application/Fhir/`, `src/ui/iPath.Blazor.ServiceLib/Fhir/` | pluggable resource loading; the HTTP loader is the seed of the Snowstorm client |
| `Hl7.Fhir.*` (R4) | already referenced | `ValueSet`, `CodeSystem`, `Coding`, parameters |
| MCP precedent | `.mcp.json` (MudMCP via `dnx`), solution `iPath.NET.slnx` (.NET 10) | repo already ships a repo-scoped MCP |

Current gap: SNOMED is referenced in only **two** files solution-wide
(`AnswerConcept.cs`, `QuestionnaireItemIndex.cs`), so there is no in-app terminology
service today. This would be net-new capability, not a refactor.

## Open questions (to settle during brainstorming, not now)

1. **Packaging** — one project with three entry points, or a core library + three thin
   projects? Where does it live: this repo, or a separate shared repo used by the other
   C# apps?
2. **MCP SDK** — official `ModelContextProtocol` C# SDK vs hand-rolled stdio JSON-RPC.
3. **MCP transport** — stdio only, or also HTTP/SSE for remote agents?
4. **Editions** — do we support multiple editions/extensions explicitly, or pin to the
   International edition and reject others?
5. **ECL** — how much ECL? Delegate to Snowstorm, or evaluate locally over a cached
   concept graph? This decides whether we need a local RF2 index at all.
6. **Local index vs server** — is the durable answer a local RF2 index (SQLite FTS5 +
   is-a closure) so we stop depending on a shared, flaky container? The degraded Snowstorm
   is the argument for it.
7. **Licence** — confirm redistribution/embedding rights for any packaged terminology.
8. **Reuse vs the Python MCP** — replace it, or keep both while the C# one matures?

## Not in scope for the idea

- No code, no project scaffold, no NuGet additions.
- The questionnaire SNOMED pass does **not** wait for this: it was unblocked via the MCP
  `remote` backend and the results are recorded in
  `doc/jundt/Clinical Informations/CLINICAL INFORMATIONS zu Spez. Lokalisationen.notes.md`.
