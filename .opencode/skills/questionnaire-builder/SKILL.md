---
name: questionnaire-builder
description: Turn free-form, pathologist-written markdown (e.g. under import/fhir/Questionnaires/doc/) into FHIR R4 Questionnaire resources for iPath.NET. Use when asked to build, generate, create, or convert a FHIR questionnaire, process pathologist descriptions or case-reporting specs, resolve SNOMED CT codes, or work with the jundt docs. Uses a Snowstorm SNOMED CT server as a semantic assistant. Produces a human-reviewed, staged pipeline: per-doc notes -> SNOMED coding -> minimal FHIR blocks -> composition -> approval.
---

# Questionnaire Builder

Convert free-form, pathologist-written markdown into FHIR R4 `Questionnaire`
resources for iPath.NET. This skill encodes a strict, gated workflow. The whole
point is **human-in-the-loop medical correctness**: never let a misinterpretation
silently become a questionnaire.

## When to use

Use this skill when the task involves:

- building / generating / creating FHIR Questionnaires for iPath.NET;
- processing pathologist descriptions or case-reporting specs (markdown under
  `import/fhir/Questionnaires/doc/`);
- resolving medical terms to SNOMED CT codes via Snowstorm;
- working with the jundt source files, the questionnaire registry, or the
  building-blocks library.

## Where things live

```
import/fhir/Questionnaires/
├── doc/<author>/          # pathologist originals (NEVER edit) + <name>.notes.md companions
├── blocks/
│   ├── blocks.json        # manifest (id, category, topography, source, file, status)
│   ├── coding-registry.json  # canonical coded question defs — alignment enforcement
│   └── 01 Material|02 Symptoms|03 Imaging|04 Lab/<id>.json
│                          # each block IS a minimal FHIR R4 Questionnaire
├── registry.json          # memory/TOC of composed questionnaires + status
├── staging/<id>/          # per-questionnaire artifacts awaiting review
└── approved/              # reviewed questionnaires, ready for manual import
```

Reference files in this skill:
- `reference/block-schema.md`  — block = minimal FHIR Questionnaire + manifest + registry contract
- `reference/fhir-conventions.md` — FHIR R4 output conventions (iPath/LHC-Forms compatible)
- `reference/snowstorm.md`      — Snowstorm endpoint patterns

Pathologist design decisions / open questions for the jundt docs live in
`import/fhir/Questionnaires/doc/jundt/FEEDBACK.md` (index: `doc/jundt/README.md`).

## Core rules (non-negotiable)

1. **Output language is English only**, regardless of what the pathologist wrote.
2. **Reuse first.** Read `registry.json` and `blocks/blocks.json` before authoring
   anything. If an existing questionnaire overlaps, say so. If a block matches,
   inline it (with a per-questionnaire `linkId` prefix) instead of regenerating.
   Blocks carry an **advisory `topography` scope**: when composing a
   questionnaire for a case with ICD-O scope, prefer blocks whose topography
   matches. Advisory only — never auto-applies an iPath BodySiteFilter.
3. **Traceability.** Every FHIR item must trace to a source line or an approved
   block. No invented content.
4. **SNOMED via Snowstorm only.** Never fabricate a concept ID. Code an answer
   only when the concept is unambiguous and the display text fits the question;
   otherwise use plain text or a local custom code.
5. **Do not skip gates.** Phase 0 always stops for clarification. Nothing is
   generated until the pathologist answers.
6. **Do not touch the running application.** No iPath code changes, no DB writes,
   no auto-import. Output goes to `staging/` and `approved/` only.
7. **ICD-O BodySiteFilter is out of scope.** You may record `icdOScope` in the
   intent (advisory, from the source doc) but the filter is set manually in the
   iPath admin UI after import.
8. **Originals are immutable.** Never edit the pathologist's source markdown.
   All internal thinking (structure, questions, SNOMED lookups, refinements,
   target blocks) goes in a readable companion `<name>.notes.md` next to the
   source. FHIR blocks are compiled only once the notes are settled.
9. **Blocks are minimal FHIR Questionnaires.** Each block file is a valid,
   standalone R4 `Questionnaire` the pathologist can load in LHC Forms to
   discuss details, and a blueprint for larger composed forms.
10. **One canonical coding per question.** Shared questions (same linkId across
    blocks) must carry identical text/type/SNOMED/LOINC/units, enforced via
    `blocks/coding-registry.json` + the validator. Composed forms dedupe
    identical questions (region/richer variant wins).

## Workflow

Run the phases in order. Never skip ahead.

### Phase 0 — Intake & notes (GATE)

1. Read the source markdown file(s) in `doc/<author>/` (read-only).
2. Read `registry.json`, `blocks/blocks.json`, `blocks/coding-registry.json`.
3. Write a companion `<name>.notes.md` next to the source with sections:
   `## Structure`, `## Questions for Jundt`, `## SNOMED lookups`,
   `## Refinements`, `## Blocks`.
4. **STOP.** Present the notes and ask the clarifying questions. Do not compile
   FHIR until the notes are settled.

### Phase 1 — SNOMED resolution (into notes)

1. For each question/answer where a clean concept may exist, query Snowstorm
   (see `reference/snowstorm.md`).
2. Decide per item: SNOMED `valueCoding` vs plain text vs local custom code.
3. Record every lookup and rejected candidate in the doc's `.notes.md`.
4. Add each resolved question to `blocks/coding-registry.json` (canonical id).

### Phase 2 — Block compilation

1. Compile each settled doc into a minimal FHIR R4 `Questionnaire` block under
   `blocks/<category>/<id>.json` per `reference/block-schema.md`.
2. Register the block in `blocks/blocks.json` (category, topography, source).
3. Validate: parses as R4; `linkId` unique; `enableWhen` targets exist; `choice`
   has `answerOption`; `quantity` has unit options; and registry consistency
   (same canonical id ⇒ identical text/type/coding/units across blocks).

### Phase 3 — Composition

1. Compose target questionnaires (e.g. Case Descriptions) by inlining blocks;
   prefix linkIds with the block id (e.g. `lab.blood-count.erythrocytes`) and
   rewrite `enableWhen` references with the same prefix.
2. Dedupe identical questions across inlined blocks (region/richer variant wins).
3. Write `staging/<id>/<id>.json` and validate per `reference/fhir-conventions.md`.

### Phase 4 — Review & approval

1. Ask the reviewer (pathologist / maintainer) to open **`viewer/index.html`** in
   the browser to inspect the interactive questionnaire, working notes, and
   original source side-by-side via LHC-Forms.
2. If new blocks, notes, or staged forms are created, run:
   ```powershell
   .\import\fhir\Questionnaires\viewer\build-bundle.ps1
   ```
   to update `viewer/data.js` so the viewer renders the latest changes.
3. On feedback: apply edits, update `.notes.md` / `FEEDBACK.md`, regenerate FHIR,
   re-run `build-bundle.ps1`, and iterate.
4. On approval: move the artifacts to `approved/`, update `registry.json`
   (`status: approved`, record `blocksUsed`), and note readiness for manual
   import into the iPath admin UI (upload + assign + set BodySiteFilter).

### Maintenance & versioning (feedback loop)

The workflow is iterative with the pathologist. When feedback arrives on an
approved/staged questionnaire (e.g. from review session notes like `doc/jundt/meeting-*.md`):

1. Apply the feedback to the companion `<name>.notes.md` and `doc/<author>/FEEDBACK.md`.
2. If the feedback touches content owned by a building block, update the block
   file and the questionnaires that inline it.
3. Bump the questionnaire **version** (semver) and create a new version
   (`staging/<id>@<version>/` artifacts) rather than silently editing the
   approved one. Keep old versions in `registry.json` (with `deprecated: true` if superseded).
4. Run `.\import\fhir\Questionnaires\viewer\build-bundle.ps1` to refresh the Review Studio.
5. Update `registry.json` and the block manifest accordingly.

Standalone "review vehicle" questionnaires: when a source doc is primarily
building-block material, still create the blocks **and** a composed questionnaire
that uses them, so the pathologist can review the composition and tell you what
belongs where.

## Status flow

`proposed -> intent -> staged -> approved -> imported`

Update `registry.json` at every transition. The registry is the agent's memory:
always read it first, always update it after.

## Execution mode

- **Now:** one agent (the current session's agent) runs the whole skill.
- **Future:** orchestrator/worker split — a cloud model orchestrates the
  workflow phases; a local model executes the mechanical per-phase steps.
  Not built yet; do not assume multi-agent tooling exists.

## Verification checklist (before claiming done)

- [ ] Phase 0 report written and clarification answered by a human
- [ ] Every intent question has a `sourceLine` or `block` reference
- [ ] No fabricated SNOMED codes (all from Snowstorm responses)
- [ ] FHIR JSON parses and validates (id, title, linkId uniqueness, enableWhen targets)
- [ ] English output throughout
- [ ] Registry updated; artifacts staged (or approved)
