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
- Conditional reveal = a **child item** of the question it belongs to, with `enableWhen`
  referencing that parent (`symptoms`: `pain` → `pain.duration`; `material`: `cytology` →
  `cytology.types`). Children carry the detail, so composed forms render them as
  sub-lines in the text preview.
- **One level only — children must be leaves, never grandchildren.** The text preview
  reads one level of children below each question, so a question that itself has children
  must sit directly under a group, never inside another question's `item[]`.
  - Nest: `pain` → `pain.duration`; `cytology` → `cytology.types`;
    `granulocytes` → `neutrophils`/`eosinophils`/`basophils`.
  - Do **not** nest a set of measurements behind one gate when one of them is itself a
    parent. `available` → `granulocytes` → `neutrophils…` renders the granulocyte total
    but silently drops the differential — the abandoned `lab.json` shape. Model the set as
    siblings carrying `enableWhen` on the gate, which is what `lab.blood-count.json` does.
  - Known violation to fix: `menopause` → `regularcycle` → `irregularities` is a
    grandchild, so `irregularities` never renders.
- Reveal tied to **one specific option** of a multi-select stays a flat sibling with
  `enableWhen` `answerString` on that option (e.g. `cytology.exfoliative`, gated by the
  `Exfoliative cytology` option of `cytology.types`). It cannot be a child of it, because
  it belongs to a single answer rather than to the question. Use `=`, not `exists` —
  LHC-Forms multi-select bug.

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
