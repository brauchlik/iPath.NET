# Block schema — minimal FHIR Questionnaires

A **block** is a minimal, standalone FHIR R4 `Questionnaire` that the
pathologist can load in NLM LHC Forms to discuss a single topic (e.g. blood lab,
gastro symptoms), and that serves as a blueprint for larger composed forms.

## Block file (`blocks/<category>/<id>.json`)

```jsonc
{
  "resourceType": "Questionnaire",
  "id": "symptoms.digestive",        // kebab-case block id
  "status": "draft",
  "title": "Symptoms — Digestive organs (C15-C26)",
  "subjectType": ["Patient"],
  "item": [ /* questions, canonical short linkIds */ ]
}
```

- Item `linkId`s are **canonical and short** (`fever`, `erythrocytes`,
  `pain.abdominal`). Composed forms prefix them with the block id
  (`lab.blood-count.erythrocytes`) and rewrite `enableWhen` references.
- `sourceLine` traceability is kept as a custom extension on each item:
  `{ "url": "http://ipath.local/sourceLine", "valueInteger": <line> }`.
- Multi-select = `choice` + `repeats: true`.
- "if yes, upload …" = `boolean` + conditional `attachment` child (`enableWhen`).
- Nested reveal (e.g. exfoliative sites) = child `choice` with `enableWhen`
  `answerString` on the parent option (use `=`, not `exists` — LHC-Forms
  multi-select bug).

## Manifest (`blocks/blocks.json`)

One entry per block: `id`, `title`, `category` (Material|Symptoms|Imaging|Lab),
`topography` (advisory ICD-O scope), `source` (doc path), `status`, `file`,
`when` (composition guidance), optional `note`.

## Coding registry (`blocks/coding-registry.json`)

Canonical question definitions keyed by linkId:
`{ text, type, snomed: [code, display]?, loinc: [code, display]?, units: [ucum…],
answerOption: [strings], repeats? }`.

- Every block item whose linkId is in the registry must match its
  text/type/snomed/loinc/units/answerOption — this is how "same question in two
  blocks ⇒ same SCT coding" is enforced.
- New questions are added here during SNOMED resolution (Phase 1), then blocks
  reference them.
- Conditional children (`pain.duration`, `weightloss.kg`) are registry entries
  too; blocks may attach them but must match the registry definition.

## Composition rules

- Inline blocks in section order (Material → Symptoms → Imaging → Lab by
  default; order may change).
- Prefer blocks whose `topography` overlaps the form's `icdOScope` (advisory).
- Dedupe identical canonical questions across inlined blocks; the region/richer
  variant wins (e.g. digestive `weightloss` with kg over general boolean).

## Case Description composition pattern

Case Description forms follow a fixed composition pattern:

```
Material (always)        → material block
+ Symptoms (always)      → organ-specific symptoms block (e.g. symptoms.digestive)
+ Imaging (optional)     → general imaging block (or organ-specific if available)
+ Lab (optional)         → lab.blood-count for hematology, or other lab blocks
```

**Do NOT include:**
- Diagnostic Assessment content (margin status, tumor staging) → that's `FinalAssessment`
- Wizard-managed fields (age, gender, ICD-O, institute) → handled by the wizard
- Follow-Up content (treatment response) → that's `FollowUp`

**Example compositions:**
| Form | Material | Symptoms | Imaging | Lab |
|---|---|---|---|---|
| Hematology/Lymph | ✓ | ✓ (general) | — | ✓ (blood count) |
| Digestive | ✓ | ✓ (digestive) | ✓ (general) | — |
| Respiratory | ✓ | ✓ (respiratory) | ✓ (general) | — |
| Breast & Gyn | ✓ | ✓ (breast/female) | ✓ (mammography) | — |
