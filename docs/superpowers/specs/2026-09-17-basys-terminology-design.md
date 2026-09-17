# Basys.Terminology — a reusable terminology client (design capture)

> **Status: proposed, decisions taken, not implemented.** Captured 2026-09-17. Extends and supersedes
> the parking note `2026-09-17-snomed-terminology-service-idea.md` (which records *why* this came up);
> this file records *what* we will build and how it is packaged. No code exists for anything here.
>
> **Separate workstream** from the answer-extraction/export work
> (`2026-09-17-sdc-answer-extraction-design.md`, `2026-09-17-export-profiles-design.md`). Do not
> interleave the two.
>
> The five items in §13 are *proposals* with a recommendation each — confirm or change them before
> implementation starts.

## 1. Purpose

Every C# project at basys that needs SNOMED CT ends up scripting HTTP against whatever terminology
server is around, with hand-picked timeouts and no idea whether a code it got back is valid. Today
that is even true inside this repo: SNOMED is referenced in exactly two files
(`QuestionnaireItemIndex.cs:15`, `AnswerConcept.cs:16`), both only as a system URI, and the actual
work is done by an external Python MCP server talking to a local Snowstorm.

Build the capability **once, in C#, as a library** — deterministic, testable, no LLM in the loop —
and let three kinds of consumer share it: other C# applications (DI), code agents (stdio MCP), and
LLM hosts (`Microsoft.Extensions.AI` tools).

The rule that shapes everything: **honest failure**. A degraded server must produce a clear error,
never a plausible but unverified code. No fabricated SCTIDs, ever.

## 2. Scope

**It is a client.** Search, resolve, validate, traverse, and (server-side) evaluate ECL.

**It is not the terminology.** No SNOMED CT content is bundled, cached to disk, or redistributed —
see §11. A future local RF2 index would ship only the importer; the operator loads their own
licensed release.

## 3. The interface: FHIR first, Snowstorm behind capabilities

The public contract is **FHIR R4 terminology**, so any conformant server can satisfy it (Snowstorm's
FHIR facade, Ontoserver, HAPI, Azure Health Data Services, …). Snowstorm's browser API — which is
richer than its FHIR facade — sits behind capability interfaces, so a caller that needs ECL or
edition details can ask for them and fail cleanly when the backend cannot provide them.

Operations:

| Operation | FHIR R4 shape | Notes |
|---|---|---|
| Term search | `ValueSet/$expand` / `CodeSystem` search | must return **concept id, PT, FSN, semantic tag, active/retired, synonyms, edition/module** — code + display only is exactly why codes get picked wrongly |
| Concept by code | `CodeSystem/$lookup` | descriptions, parents/ancestors, children |
| Validate | `$validate-code`, plus our own checks | is it active in *our* edition, and does the semantic tag match the expected type (finding vs procedure vs observable vs body structure)? |
| Subsumption | `CodeSystem/$subsumes` | is-a tests |
| Hierarchy | `$lookup` + ancestors/descendants | reuse the closure logic we already have (§8) |
| ECL | server-side only | decision 7: delegate; keep our code simple |

## 4. Package family

```
Basys.Terminology            core: interfaces, DTOs, result types, options, DI + Aspire overload   (no iPath deps)
Basys.Terminology.Snowstorm  /browser/{branch}/descriptions, /concepts/{id}, ECL, subsumes
Basys.Terminology.Fhir       $lookup, $validate-code, $expand, $subsumes — the portable backend
Basys.Terminology.Mcp        stdio MCP host (PackAsTool); tools in a shared TerminologyTools class
Basys.Terminology.AI         AIFunction tools (thin)
```

- `Basys.Terminology` is the package everyone references; backends and adapters are opt-in.
- `net10.0` and newer only (decision 4).
- The core must **never** reference `iPath.*` — that is the only thing that makes the later move to
  its own repo (decision 3) mechanical.
- Namespaces `Basys.Terminology[.Snowstorm|.Fhir|.Mcp|.AI]`, package family `Basys.Terminology.*`
  (decision 2).

## 5. Cross-cutting concerns

1. **HTTP**: `IHttpClientFactory` + `Microsoft.Extensions.Http.Resilience` — already the house choice
   (`aspire/iPath.NET.ServiceDefaults` references 10.10.0), so no new dependency philosophy.
2. **Timeouts**: generous and configurable. Measured reality: 1–20 s per call, ~21 s for by-code
   lookups; the Python MCP's 10 s search / 30 s expand timeouts are what made it unusable even on a
   healthy server.
3. **Caching**: `IMemoryCache` by default, injectable so a host can supply a distributed cache.
   By-code lookups and search results both; caching is not optional at those latencies.
4. **Honest failure**: result types with an explicit reason (`NotFound`, `Inactive`, `Unavailable`,
   `EditionMismatch`, `InvalidInput`) rather than exceptions for expected outcomes — same spirit as
   the existing `CodeLookupResult` (§8). Truly exceptional conditions still throw.
5. **Observability**: `ILogger` throughout, plus an `ActivitySource`/`Meter` so Aspire and
   OpenTelemetry pick it up like everything else.
6. **Configuration**: a `TerminologyOptions` class bound via `IOptions<T>` per AGENTS.md
   (`BaseUrl`, `Branch`, `Edition`, `Language`, timeouts, cache TTLs) — no magic strings.
7. **Aspire**: an `AddTerminologyClient("snowstorm")` overload that uses service discovery, so a host
   AppHost can inject the base address instead of hard-coding `http://bassysrvdock1:8082`.

## 6. The tool layer (why hosting later is cheap)

```
ITerminologyService            ← the only real logic (library, unit-tested)
TerminologyTools               ← plain class; its methods are the MCP tools and the AIFunctions
   ├── stdio host   (console app, official MCP SDK)         Phase 2, thin
   ├── AIFunction   (Microsoft.Extensions.AI)               Phase 2, thin
   └── HTTP host    (ASP.NET Core, streamable HTTP MCP)     later, mostly hosting
```

Decision 8: stdio is enough now. A web-hosted variant later is a **new host project**, not a
rewrite, provided the tool layer stays stateless — no shared mutable state, everything through
injected options and services. What actually costs time in the HTTP case is authentication, per-caller
credentials/edition, rate limiting and deployment; hosting inside iPath would inherit its login and
roles, which is what makes it attractive later.

Development convenience: `.mcp.json` can run the MCP straight from the repo
(`dotnet run --project src/terminology/Basys.Terminology.Mcp -- --stdio`) — the same shape as the
existing `dnx MudMCP --stdio` entry — so no feed is needed while developing. Once published as a
`.NET tool`, agents can use `dnx Basys.Terminology.Mcp --stdio`.

## 7. Phases

### Phase 1 — core + Snowstorm + FHIR (the next actionable deliverable)

- [ ] Create `src/terminology/` projects and add them to `iPath.NET.slnx`.
- [ ] `Basys.Terminology`: `ITerminologyService`, DTOs, result types, `TerminologyOptions`, `AddTerminology` (+ Aspire overload).
- [ ] `Basys.Terminology.Snowstorm`: descriptions search, concept by code, ECL, subsumption, mapping Snowstorm's JSON onto our model.
- [ ] `Basys.Terminology.Fhir`: `$lookup`, `$validate-code`, `$expand`, `$subsumes` via `Hl7.Fhir.R4` (already referenced in the solution).
- [ ] Caching, resilience, logging, `ActivitySource`.
- [ ] Test project: stub `HttpMessageHandler` + fixtures captured from the live server; contract tests for the quirks in §9; opt-in integration tests.

### Phase 2 — adapters

- [ ] `Basys.Terminology.Mcp`: `TerminologyTools` + stdio host, `PackAsTool`; add a repo `.mcp.json` entry running it from source.
- [ ] `Basys.Terminology.AI`: `AIFunctionFactory` tools over the same `TerminologyTools`.
- [ ] Update `.opencode/skills/questionnaire-builder/reference/snowstorm.md` to point at the C# tool; keep the Python MCP until the C# one has proven itself (idea note, open question 8).

### Phase 3 — iPath integration

- [ ] Adapter mapping the library onto the existing `CodingService` / `CodeSystemLookup` / `CodeLookupResult` types, so the app keeps its own domain types.
- [ ] A config class (`SnowstormConfig` or similar) registered and bound per AGENTS.md, replacing any hard-coded base URL.
- [ ] Use it for the questionnaire SNOMED pass that the skill currently does through the Python MCP.

### Phase 4 — packaging and distribution

- [ ] `PackAsTool` for the MCP, `IsPackable` + metadata for the libraries, per-package `PackageVersion` starting at 0.1.0 (independent of the app's version in `Directory.Build.props`).
- [ ] `pack.ps1` pushing to the chosen feed; `NuGet.config` in the repo listing the source.
- [ ] CI later (there is none today — only `.github/copilot-instructions.md`).
- [ ] Own repo / public feed only if it proves worth it (decision 3).

## 8. Existing assets to reuse (verified in this repo)

| Asset | Path | Reuse |
|---|---|---|
| `CodeSystemLookup` | `src/core/iPath.Application/Coding/CodeSystemLookup.cs` | parent→child / child→parent closure with a visited set — the is-a and subsumption core. Only references `iPath.Application.Coding`/`.Fhir`, so it is extractable |
| `CodingService` | `…/Coding/CodingService.cs` | loads and caches a `CodeSystem`, wraps lookups; `InConceptFilter` already fails open when no lookup is loaded |
| `CodeLookupResult`, `CodeDisplay`, `ValueSetDisplay`, `CodeSystemTranslationService`, `ValueSetTranslationService`, `CodingExtensions` | `…/Coding/` | the result-type and display vocabulary to stay consistent with |
| `IFhirDataLoader`, `FileFhirDataLoader` | `src/core/iPath.Application/Fhir/` | pluggable resource loading |
| `HttpFhirDataLoader` | `src/ui/iPath.Blazor.ServiceLib/Fhir/` | the seed of the FHIR-terminology client |
| `Hl7.Fhir.R4` 6.5.0 | solution-wide | `CodeSystem`, `ValueSet`, `Coding`, parameters |
| `Microsoft.Extensions.Http.Resilience` 10.10.0 | `aspire/iPath.NET.ServiceDefaults` | retry/circuit-breaker conventions |
| `Microsoft.Extensions.AI` 10.10.0 | `iPath.Application`, `iPath.API` | the AIFunction adapter |
| MCP precedent | `.mcp.json` (`dnx MudMCP --stdio`) | how this repo already consumes a stdio MCP |

The platform is .NET 10 with an `.slnx` solution, an Aspire AppHost/ServiceDefaults pair, xUnit +
FluentAssertions, and `Directory.Build.props` carrying only `<Version>`.

## 9. Snowstorm specifics worth encoding as tests

From `.opencode/skills/questionnaire-builder/reference/snowstorm.md` and the idea note — these are
observed behaviours, so they belong in fixture-backed contract tests rather than in tribal memory:

- Base URL `http://bassysrvdock1:8082`, branch `MAIN`, no auth, SNOMED International edition.
- The `term` filter on `/browser/MAIN/concepts` is **ignored** (returns the whole list); use
  `/browser/{branch}/descriptions?term=…&activeFilter=true&limit=…` for search.
- The description-level `active` can be false while the concept is active — filter on
  `concept.active`, and dedupe by `concept.conceptId` (the term list contains historical/duplicate
  entries).
- `totalElements` can be huge for broad substring matches: trust the ordering, then confirm the
  winner through `/browser/{branch}/concepts/{sctid}` before committing a code.
- The search index has been degraded before (hangs). Timeouts and the "unavailable" result path are
  first-class, not an afterthought.

## 10. Testing

- **Unit/contract**: stub `HttpMessageHandler` replaying captured fixtures for the endpoints above —
  including the quirks in §9. This is what catches Snowstorm API drift in `dotnet test`.
- **Behaviour**: search ranking hints, semantic-tag validation, active/retired handling, edition
  mismatch, cache hit/miss, timeout → `Unavailable`, and explicitly: a failure must never return a
  code.
- **Integration**: opt-in via an environment variable against the real server, skipped by default
  (the suite already skips integration tests today).

## 11. Licensing and naming

- SNOMED CT is licensed, and licensing is **per country** (AFTS/member territories), with trademark
  protection. Therefore: ship **client code only**, never content; document that every consumer needs
  their own licence/membership; do not imply SNOMED International endorsement.
- A local RF2 index stays licence-clean as long as we ship only the **importer** and the operator
  loads their own licensed release — that is the framing if a local index ever becomes a requirement
  (§12, decision 5).
- Consequence: neutral package naming under our own `Basys.*` brand, and a private feed first;
  public distribution only with a README/NOTICE stating all of the above.

## 12. Decisions taken (2026-09-17)

| # | Question | Decision |
|---|---|---|
| 1 | Who reuses it? | basys projects — hence the `Basys.Terminology` name |
| 2 | Where does it live? | Developed in this repo; own repo/public feed only if it proves worth it |
| 3 | Target frameworks | `net10.0` and newer |
| 4 | Interface style | **FHIR first**, Snowstorm behind capability interfaces |
| 5 | Backend | Snowstorm for now; a local RF2 index stays an option (see §11) |
| 6 | ECL | Delegated to the server — our code stays simple |
| 7 | Consumer #2 | stdio MCP is enough for code agents; a web-hosted variant is a later, separate host project |
| 8 | Sequencing | Separate workstream from the answers/export work |
| 9 | The idea note | Committed alongside this document |

## 13. Open items — proposals to confirm

1. **Feed**: a folder/UNC source first (`dotnet nuget push` + `dotnet nuget add source`), GitHub
   Packages once CI exists. *Recommendation: folder source now.*
2. **Package licence expression**: none/proprietary while the packages are private; MIT only if we
   ever publish publicly. *Recommendation: no expression while private.*
3. **Failure style**: result types with explicit reasons over exceptions for expected outcomes.
   *Recommendation: result types, consistent with `CodeLookupResult`.*
4. **Defaults to pin**: branch `MAIN`, International edition, language `en`; cache TTL 24 h for
   by-code and 1 h for search results. *Recommendation: as stated, all configurable.*
5. **Phase boundary**: detailed planning for Phase 1, sketch for Phases 2–4 (as written above), or
   fully detailed tasks for all phases before starting. *Recommendation: Phase 1 detailed, rest
   sketched.*

## 14. Out of scope

A local RF2 index (importer only, later — §11) · ECL evaluation in our code · SNOMED content of any
kind · a public service or SaaS offering · terminology other than SNOMED CT (the FHIR-first contract
happens to allow LOINC/ICD-10 later, but nothing is designed for it now) · mapping/translation
between code systems · replacing the existing Python MCP before the C# one is proven.

## 15. References

- `2026-09-17-snomed-terminology-service-idea.md` — why this came up, the degraded Snowstorm, the two
  consumers, and the eight original open questions.
- `.opencode/skills/questionnaire-builder/reference/snowstorm.md` — the endpoint patterns and coding
  policy currently in use.
- `2026-08-18-fhir-questionnaire-agent-design.md` — the pipeline that will consume this.
- `2026-09-17-sdc-answer-extraction-design.md` — where the codes end up (`AnswerConcept`,
  `QuestionnaireItemIndex`).
