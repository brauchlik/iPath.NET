# FHIR R4 output conventions

The compiled output is a valid FHIR R4 `Questionnaire` that:
- parses with `Hl7.Fhir` R4 (`new JsonSerializerOptions().ForFhir(ModelInfo.ModelInspector)`),
- renders correctly in the **NIH LHC-Forms** JS library (used by iPath.NET and the
  NLM Form Builder),
- imports cleanly via the iPath admin UI (which extracts `id` -> `QuestionnaireId`
  and `title` -> `Name`).

## Blocks

Reusable sections are authored as minimal FHIR Questionnaires under
`blocks/<category>/<id>.json` (canonical short linkIds, `sourceLine` extension,
multi-select via `choice`+`repeats`), registered in `blocks/blocks.json`, and
kept coding-aligned through `blocks/coding-registry.json`. See
`reference/block-schema.md`. Composed forms inline blocks with a linkId prefix
and dedupe identical canonical questions.

## Minimum structure

```json
{
  "resourceType": "Questionnaire",
  "id": "jundt-hematology-lymph",
  "url": "http://ipath.kantonsspital.net/fhir/Questionnaire/jundt-hematology-lymph",
  "status": "draft",
  "title": "Hematology and Lymph nodes",
  "subjectType": ["Patient"],
  "item": [ /* groups and questions */ ]
}
```

- `id`: kebab-case, unique, matches the staging folder name.
- `status`: `draft` while in review; a reviewer may set `active` after approval.
- `version`: use a semver-like string when a second version is created.

## Item types (FHIR R4 `Questionnaire.item.type`)

| Intent type | FHIR item type |
|---|---|
| `boolean` | `boolean` |
| `integer` | `integer` |
| `decimal` | `decimal` |
| `quantity` | `quantity` (+ `unit` via `item.extension` for UCUM where known, or leave display unit) |
| `choice` / `open-choice` | `choice` / `open-choice` (+ `answerOption`) |
| `string` | `string` |
| `text` | `text` |
| `attachment` | `attachment` |
| section | `group` (with nested `item[]`) |

## linkId

- Unique within the questionnaire.
- Readable slugs: `lab.erythrocytes`, `imaging.ct`.
- Block-inlined questions keep their block-prefixed linkId (`imaging.ct`).

## Coded questions

- Question-level SNOMED concept -> `item.code`:
  ```json
  { "system": "http://snomed.info/sct", "code": "716690008", "display": "..." }
  ```
- Coded answers -> `answerOption.valueCoding` with the same system.
- If no clean concept exists, use plain text answers or a local code
  (e.g. `system: "http://ipath.local"`). Never invent SNOMED codes.

## Lab values (quantity + selectable unit + LOINC)

Lab counts are `quantity` items (values are decimal, e.g. RBC 4.8). Units come
from LOINC, not SNOMED; offer UCUM `questionnaire-unitOption` entries with the
most common/SI variant **first** (LHC-Forms defaults the dropdown to the first
option), and add the LOINC code as a second `item.code` coding:

```json
{
  "linkId": "lab.blood-count.erythrocytes",
  "type": "quantity",
  "text": "Erythrocytes",
  "code": [
    { "system": "http://snomed.info/sct", "code": "14089001", "display": "Red blood cell count" },
    { "system": "http://loinc.org", "code": "789-8", "display": "Erythrocytes [#/volume] in Blood by Automated count" }
  ],
  "extension": [
    { "url": "http://hl7.org/fhir/StructureDefinition/questionnaire-unitOption",
      "valueCoding": { "system": "http://unitsofmeasure.org", "code": "10*12/L", "display": "10^12/L" } },
    { "url": "http://hl7.org/fhir/StructureDefinition/questionnaire-unitOption",
      "valueCoding": { "system": "http://unitsofmeasure.org", "code": "10*6/uL", "display": "10^6/µL" } }
  ]
}
```

## Conditional uploads ("if yes, please upload images and report")

```json
{
  "linkId": "imaging.xray",
  "type": "boolean",
  "text": "Conventional X-rays (2 planes)",
  "item": [
    {
      "linkId": "imaging.xray.upload",
      "type": "attachment",
      "text": "Upload images and report",
      "enableWhen": [
        { "question": "imaging.xray", "operator": "=", "answerBoolean": true }
      ]
    }
  ]
}
```

## Validation checklist (run before staging)

- [ ] Deserializes with Hl7.Fhir R4
- [ ] `resourceType`, `id`, `title`, `status` present
- [ ] `linkId` unique across all items (known violation still to fix: `sym.other` in
      `jundt-case-digestive.json` exists both as a `string` inside `sym` and as a
      top-level `group` with 22 children)
- [ ] conditional details are **one level deep**: a question's children are leaves, and a
      question that has children sits directly under a group (a grandchild is dropped by
      the text preview - e.g. `menopause` → `regularcycle` → `irregularities`)
- [ ] a reveal bound to one multi-select option is a sibling with `enableWhen`
      `answerString` (`material`: `cytology.exfoliative`)
- [ ] a *set* of measurements behind one gate are siblings carrying `enableWhen` on the
      gate, not children of it (`lab.blood-count.json`, not `lab.json`)
- [ ] a child's `linkId` is `<parentLinkId>.<name>` and the child carries an `enableWhen`
      on its parent
- [ ] `choice`/`open-choice` items have `answerOption`
- [ ] every `enableWhen.question` linkId exists
- [ ] quantity items declare a unit
- [ ] renders without error in LHC Forms / NLM Form Builder
