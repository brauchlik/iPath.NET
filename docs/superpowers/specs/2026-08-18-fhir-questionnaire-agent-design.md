# FHIR Questionnaire Building Agent — Design

**Date:** 2026-08-18
**Status:** Approved for implementation
**Owner:** Kurt / iPath.NET

## 1. Goal

A **reusable agent skill** that turns free-form, pathologist-written markdown
descriptions into FHIR R4 `Questionnaire` resources for the iPath.NET system.
The agent uses a local **Snowstorm** SNOMED CT server as a semantic assistant to
disambiguate medical terms and encode answers with SNOMED concepts where a clear
concept exists, falling back to plain text or custom codes otherwise.

This is an **offline workflow in the repository** — it is *not* part of the
running iPath software. The questionnaires are staged in the repo, reviewed by a
human (pathologist) in the **NLM LHC Forms Form Builder**, and later imported
manually through the existing iPath admin UI.

## 2. Context (as-is)

- Questionnaires are stored as raw FHIR R4 `Questionnaire` JSON
  (`QuestionnaireEntity`: `QuestionnaireId`, `Name`, `Version`, `IsActive`,
  `Resource`, `Settings`).
- Rendered by the **NIH LHC-Forms** JS library (`src/ui/iPath.LHCForms/`).
- Filtered by ICD-O topography via `Settings.BodySiteFilter`; matching uses the
  ICD-O CodeSystem parent/child graph (`CodingService.InConceptFilter`).
- Import is manual: admin UI upload → parse as Hl7.Fhir `Questionnaire` →
  extract `id` → `QuestionnaireId`, `title` → `Name` → store.
- Usage types (`CaseDescription`, `Annotation`, `FollowUp`, `FinalAssesment`)
  are decided at assignment time in the admin, *not* inside the FHIR resource.
- Existing AI patterns: `import/CodingAgent` (SemanticKernel console app) and
  `AiExtractionService` (Microsoft.Extensions.AI + JSON structured output).

## 3. Scope

### In scope
1. A reusable agent skill under `.opencode/skills/questionnaire-builder/`.
2. A questionnaire **intent model** (intermediate representation) with
   source-line traceability.
3. A **building blocks library** of reusable intent-level groups.
4. A **registry** (agent memory / TOC) of all questionnaires and their status.
5. A staged, human-reviewed output pipeline (intent → SNOMED → FHIR → approval).
6. First pilot run: the two files in `import/fhir/Questionnaires/doc/jundt/`.

### Out of scope
- ICD-O `BodySiteFilter` generation (set manually in the admin UI after import).
- Automatic import into iPath (remains a manual admin step).
- Multi-agent / orchestrator-worker execution (documented as a future option).
- Any changes to the running iPath.NET application.

## 4. Repo layout & artifacts

```
import/fhir/Questionnaires/
├── doc/<author>/          # pathologist source markdown (existing: doc/jundt/)
├── blocks/                # reusable intent-level building blocks
│   ├── imaging.general.json
│   ├── imaging.jaw.json
│   ├── imaging.breast.json
│   ├── lab.blood-count.json
│   └── blocks.json        # block manifest (id, title, when to use)
├── registry.json          # memory/TOC: every questionnaire, status, reuse notes
├── staging/               # per-questionnaire artifacts awaiting review
│   └── <id>/
│       ├── <id>.review.md      # Phase-0 validation + clarification report
│       ├── <id>.intent.json    # Phase-1 structured interpretation
│       ├── <id>.snomed.md      # Phase-2 Snowstorm lookup log + uncertainty
│       └── <id>.json           # Phase-3 FHIR R4 Questionnaire
└── approved/              # reviewed questionnaires, ready for manual import
```

Skill location: `.opencode/skills/questionnaire-builder/`

```
.opencode/skills/questionnaire-builder/
├── SKILL.md                    # the workflow (Phases 0–4, gates, rules)
└── reference/
    ├── intent-schema.md        # intent JSON contract
    ├── fhir-conventions.md     # FHIR R4 output conventions
    └── snowstorm.md            # Snowstorm endpoint patterns
```

Per-questionnaire artifacts (kept small on purpose):

| Artifact | Produced | Purpose |
|---|---|---|
| `<id>.review.md` | Phase 0 | Agent's understanding, ambiguities, clarifying questions, reuse suggestions. **Gate.** |
| `<id>.intent.json` | Phase 1 | Machine-readable interpretation; reviewed for *medical* correctness. |
| `<id>.snomed.md` | Phase 2 | Lookup log: terms searched, codes chosen, uncertainty. Auditable coding. |
| `<id>.json` | Phase 3 | Compiled FHIR R4 Questionnaire; reviewed in the Form Builder. |

## 5. Workflow phases (the skill's core)

The skill enforces these phases and gates. The agent must not skip ahead.

### Phase 0 — Intake & validation (gate)
1. Read source markdown from `doc/<author>/`.
2. Read `registry.json` (reuse check) and `blocks/blocks.json` (block match).
3. Produce `<id>.review.md`:
   - structure as understood by the agent;
   - ambiguities and missing information (units, required flags, value ranges,
     ICD-O scope, titles);
   - clarifying questions for the pathologist;
   - reuse suggestions ("a Hematology questionnaire already exists; overlap ~80%");
   - proposed `<id>` (kebab-case) and language-normalization notes.
4. **STOP.** Ask the pathologist. No intent/FHIR is produced until answered.

### Phase 1 — Intent modeling
- Produce `<id>.intent.json` per the intent schema (Section 7).
- Inline matching building blocks from the library with a per-questionnaire
  `linkId` prefix (e.g. `imaging.ct`) instead of re-authoring.

### Phase 2 — SNOMED resolution
- For each question/option where a clean concept may exist, query Snowstorm.
- Decide per item: `valueCoding` (SCT) vs plain text vs local custom code.
- Record all lookups and uncertainty in `<id>.snomed.md`.
- **Never fabricate a concept ID** — only codes returned by the server.

### Phase 3 — FHIR compilation
- Compile `intent.json` → FHIR R4 `Questionnaire` JSON per `fhir-conventions.md`.
- Validate: parses as Hl7.Fhir `Questionnaire`; `id`/`title` present; `linkId`
  unique; types/answerOption consistent; `enableWhen` targets exist.

### Phase 4 — Review & approval
- Pathologist loads `<id>.json` into the NLM LHC Forms Form Builder.
- Feedback loop: edits → agent regenerates intent/FHIR.
- On approval: move to `approved/`, update `registry.json`, mark blocks used.
- Manual import into the iPath admin UI remains the final step.

## 6. Building blocks library

- `blocks/*.json` hold **intent-level** reusable groups (not FHIR fragments), so
  they stay editable and re-codable.
- Each block: `id`, `title`, questions (types/options/units), a "when to use"
  hint in the manifest.
- Policy: check `blocks.json` before authoring new questions; reuse instead of
  regenerate. On reuse, inline with a per-questionnaire `linkId` prefix.
- First-run candidates extracted from the jundt docs:
  - `imaging.general` (X-ray, sonography, CT, MRI, PET variants + upload)
  - `imaging.jaw` (panoramic view, CBCT; site-specific, C41.1/C41.0)
  - `imaging.breast` (mammography)
  - `lab.blood-count` (hematology lab values)

## 7. Intent schema (`<id>.intent.json`)

Lean JSON capturing intent before FHIR exists. Every question carries its
**source line** from the original markdown for traceability.

```jsonc
{
  "id": "jundt-hematology-lymph",
  "title": "Hematology and Lymph nodes",
  "icdOScope": ["C42", "C77"],   // advisory only; filter set in admin
  "source": "doc/jundt/Hematology [C42-C42] and Lymph nodes [C77].md",
  "status": "intent",            // proposed | intent | staged | approved | imported
  "sections": [
    {
      "linkId": "lab",           // group = questionnaire.item of type "group"
      "title": "Labor",
      "questions": [
        {
          "sourceLine": 3,
          "text": "Blood cell count available",
          "type": "boolean",
          "required": false,
          "coding": null          // filled in Phase 2, or stays null
        },
        {
          "sourceLine": 4,
          "text": "Erythrocytes",
          "type": "integer",      // mapped from "Type a Number"
          "unit": null
        }
      ]
    }
  ],
  "blocksUsed": ["lab.blood-count"]
}
```

Type mapping (encoded in the skill):
`Yes/No` → `boolean` · `Type a Number` → `integer`/`decimal` ·
`if yes, please upload images and report` → `boolean` + conditional
`attachment` item via `enableWhen` · free text → `string`/`text` ·
enumerated lists → `choice` with `answerOption`.

## 8. Registry (`registry.json`, agent memory)

Read at the start of every run; used to answer "have we built something like
this already?" and suggest reuse. Updated at every status change.

```jsonc
{
  "questionnaires": [
    {
      "id": "jundt-hematology-lymph",
      "title": "Hematology and Lymph nodes",
      "status": "staged",          // proposed → intent → staged → approved → imported
      "source": "doc/jundt/Hematology [C42-C42] and Lymph nodes [C77].md",
      "icdOScope": ["C42", "C77"],
      "blocksUsed": ["lab.blood-count"],
      "created": "2026-08-18"
    }
  ]
}
```

## 9. Snowstorm usage

- Base URL: `http://basyssrvdock1:8082`, no auth.
- Primary: `GET /browser/MAIN/concepts?term=<term>&activeFilter=true&limit=10`.
- Confirm a candidate via `GET /browser/MAIN/concepts/<sctid>` (FSN, parents,
  semantic tag) before committing a code.
- Rule: code an answer only when the concept is unambiguous and the display
  text fits the question; otherwise plain text or a local custom code.
- Never invent concept IDs. Full patterns in `reference/snowstorm.md`.

## 10. FHIR output conventions

- Valid R4 `Questionnaire`; `id` kebab-case (→ iPath `QuestionnaireId`);
  `title` (→ iPath `Name`); `status: draft`.
- Sections as `group` items; SNOMED codings on `item.code` (question) and
  `answerOption.valueCoding` (answer); `enableWhen` for conditional uploads;
  `unit` on quantity items.
- Must parse with Hl7.Fhir and render in LHC Forms.
- Full conventions in `reference/fhir-conventions.md`.

## 11. Execution mode

- **Now:** single agent (the agent configured in the OpenCode session) runs the
  whole skill.
- **Future (documented, not built):** orchestrator/worker split — a cloud model
  (e.g. DeepSeek) orchestrates the workflow and a local model (e.g. Qwen via
  Ollama) executes the mechanical per-phase steps.

## 12. First run (jundt pilot)

Proves the loop end-to-end with `import/fhir/Questionnaires/doc/jundt/`:

1. Phase 0 on `Hematology [C42-C42] and Lymph nodes [C77].md` and `imaging.md`
   → review reports + clarifying questions. **Agent stops and asks.**
2. After answers → intent files → Snowstorm lookups → FHIR R4 JSON → staged.
3. Human reviews in the NLM Form Builder; iterate.
