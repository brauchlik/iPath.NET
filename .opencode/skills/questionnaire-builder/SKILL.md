---
name: questionnaire-builder
description: Turn free-form, pathologist-written markdown (e.g. under import/fhir/Questionnaires/doc/) into FHIR R4 Questionnaire resources for iPath.NET. Use when asked to build, generate, create, or convert a FHIR questionnaire, process pathologist descriptions or case-reporting specs, resolve SNOMED CT codes, or work with the jundt docs. Uses a Snowstorm SNOMED CT server as a semantic assistant. Produces a human-reviewed, staged pipeline: per-doc notes -> SNOMED coding -> minimal FHIR blocks -> composition -> approval.
---

# Questionnaire Builder

Convert free-form, pathologist-written markdown into FHIR R4 `Questionnaire`
resources for iPath.NET. This skill encodes a strict, gated workflow. The whole
point is **human-in-the-loop medical correctness**: never let a misinterpretation
silently become a questionnaire.

## Our role

We are **medical form designers** for iPath.NET. Our job is to:

1. **Decompose** the pathologist's "general picture" into separate form types
   that match iPath's software design.
2. **Classify** each piece of content by its proper form type (Case Description,
   Diagnostic Assessment, Follow-Up, Annotation).
3. **Compose** forms from reusable building blocks, following composition rules.
4. **Integrate** with iPath's workflow: different forms are filled by different
   people at different times.

The pathologist (Dr. Jundt) typically describes everything together — material,
symptoms, imaging, lab values, diagnostic assessment, treatment history. He
doesn't distinguish between "what I need to create a case" and "what I need to
assess it later." **That separation is our job.**

## iPath form types

iPath uses `eQuestionnaireUsage` to classify questionnaires. Each type is used
at a different point in the workflow, filled by different people:

| Usage | Value | When | Who fills it | Purpose |
|---|---|---|---|---|
| `CaseDescription` | 2 | Case creation (Wizard Step 1) | Referring physician / intake | Capture the initial clinical picture: material, symptoms, imaging, lab |
| `Annotation` | 3 | Any time after creation | Anyone (pathologist, clinician) | Free-text or structured comments |
| `FollowUp` | 4 | After initial assessment | Clinician | Track treatment response, recurrence |
| `FinalAssessment` | 5 | After microscopic examination | Pathologist | Summarize all diagnostic input + follow-up recommendations |

**Key insight:** The pathologist's "general picture" mixes content from ALL of
these. Our job is to sort it into the right buckets.

## Content classification

When processing pathologist instructions, classify each piece of content:

### Case Description (`CaseDescription`)
- Material for examination (cytology, histology, bone marrow)
- Symptoms (pain, swelling, weight loss, etc.)
- Imaging (X-ray, CT, MRI, PET — if available at intake)
- Lab values (blood count, basic chemistry)
- Clinical history relevant to the presenting complaint

### Diagnostic Assessment (`FinalAssessment`)
- Margin status (R0/R1/R2)
- Tumor size, depth, staging
- Histological subtype, grading
- Lymphovascular invasion, perineural invasion
- Resection margins
- Pathological staging (pTNM)

### Follow-Up (`FollowUp`)
- Treatment response
- Recurrence monitoring
- Interval changes
- Treatment modifications

### Annotation (`Annotation`)
- Comments, questions, notes
- Quick observations
- Second opinions

## The decomposition problem

Dr. Jundt's form suggestions typically mix:

1. **Wizard-managed fields** (age, gender, ICD-O topography, submitting institute)
   — handled by iPath, not our forms
2. **Case Description content** (material, symptoms, imaging, lab)
3. **Diagnostic Assessment content** (margin status, tumor size, staging)
4. **Clinical history** (steroid treatment, previous radiation, comorbidities)

Example from Jundt's hematology form:
```
Material (histology, blood smear, bone marrow)  → CaseDescription
Disease type (CML, CLL, MDS, etc.)              → CaseDescription (clinical info)
Steroid treatment                               → CaseDescription (clinical info)
Accidental finding                              → CaseDescription (clinical info)
Symptoms (fatigue, bleeding, infections)        → CaseDescription
Lab values (CBC, differential)                  → CaseDescription
Diagnostic assessment                           → FinalAssessment (NOT CaseDescription)
Imaging (if available at intake)                → CaseDescription
```

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
├── staging/
│   └── <Form Type>/       # organized by form type (Case Description, Diagnostic Assessment, etc.)
│       └── <id>/          # per-questionnaire artifacts awaiting review
└── approved/              # reviewed questionnaires, ready for manual import
```

Reference files in this skill:
- `reference/block-schema.md`  — block = minimal FHIR Questionnaire + manifest + registry contract
- `reference/fhir-conventions.md` — FHIR R4 output conventions (iPath/LHC-Forms compatible)
- `reference/form-types.md` — iPath form types, content classification, decomposition rules
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
11. **Respect form type boundaries.** Content classified as Diagnostic Assessment
    (`FinalAssessment`) must NOT appear in Case Description forms. If the
    pathologist includes it, note it as deferred to the appropriate form type.

## Workflow

Run the phases in order. Never skip ahead.

### Phase 0 — Intake & classify (GATE)

1. Read the source markdown file(s) in `doc/<author>/` (read-only).
2. Read `registry.json`, `blocks/blocks.json`, `blocks/coding-registry.json`.
3. **Classify content by form type.** For each piece of content in the source:
   - Is this Case Description material (material, symptoms, imaging, lab)?
   - Is this Diagnostic Assessment (margin status, tumor staging)?
   - Is this Follow-Up (treatment response, recurrence)?
   - Is this handled by the wizard (age, gender, ICD-O, submitting institute)?
   - Is this clinical history that belongs in Case Description (steroid treatment, previous radiation)?
4. Write a companion `<name>.notes.md` next to the source with sections:
   `## Structure`, `## Content Classification`, `## Questions for Jundt`,
   `## SNOMED lookups`, `## Refinements`, `## Blocks`.
5. **STOP.** Present the notes and ask the clarifying questions. Do not compile
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
   (same canonical id => identical text/type/coding/units across blocks).
4. **Nest one level only - children must be leaves.** A conditional detail is a child item
   of the question it belongs to (`pain` → `pain.duration`, `cytology` → `cytology.types`,
   `granulocytes` → `neutrophils`). A question that has its own children must sit directly
   under a group, never inside another question's `item[]`: the case-description text
   preview reads exactly one level down, so a grandchild is silently dropped. In
   particular a *set* of measurements behind one gate stays siblings carrying `enableWhen`
   (`lab.blood-count.json`), instead of being nested under the gate (`lab.json`, which
   loses the differential). The only true exception is a reveal bound to a **single
   option** of a multi-select, which also stays a sibling (see `reference/block-schema.md`).

### Phase 3 — Composition

1. Compose target questionnaires by form type:
   - **Case Description:** Material + Symptoms + optional Imaging + optional Lab
   - **Final Assessment:** Margin status, tumor size, staging, histological details + follow-up recommendations
   - **Follow-Up:** Treatment response, recurrence monitoring
2. Inline blocks with a per-questionnaire `linkId` prefix (e.g. `lab.blood-count.erythrocytes`).
3. Rewrite `enableWhen` references with the same prefix.
4. **Preserve the item tree.** Prefixing linkIds and rewriting `enableWhen` are the only
   transformations allowed - never hoist a child up to a sibling. (Precedent: `lab.json`
   nests the quantities under `available`, while `lab.blood-count.json` keeps them as
   siblings; compose from the nested shape.)
5. Dedupe identical questions across inlined blocks (region/richer variant wins).
6. Write `staging/<Form Type>/<id>/<id>.json` and validate per `reference/fhir-conventions.md`.

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
- [ ] Content classified by form type (Case Description vs Final Assessment vs Follow-Up)
- [ ] Wizard-managed fields identified and excluded (age, gender, ICD-O, institute)
- [ ] Every intent question has a `sourceLine` or `block` reference
- [ ] No fabricated SNOMED codes (all from Snowstorm responses)
- [ ] FHIR JSON parses and validates (id, title, linkId uniqueness, enableWhen targets)
- [ ] English output throughout
- [ ] Registry updated; artifacts staged (or approved)
