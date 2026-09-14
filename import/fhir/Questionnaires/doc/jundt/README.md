# jundt — questionnaire source docs

Source markdown written by the pathologist (Jundt), organised by **target**.
Originals are never edited; each has a readable `<name>.notes.md` companion that
holds our internal thinking (structure, questions, SNOMED lookups, refinements,
target blocks). FHIR blocks are compiled only once notes are settled.

## Targets

| Folder | Purpose |
|---|---|
| `Case Descriptions/` | Region-specific forms; 4 sections: `01 Material`, `02 Symptoms`, `03 Imaging`, `04 Lab` |
| `Diagnostic Assessment/` | Diagnosis fields (margin status, soft tissue, breast) — later |
| `Follow Up/` | Follow-up requests — later |
| `originals/` | The pathologist's original `.docx` files |

## Blocks

Settled notes compile to minimal FHIR Questionnaires under
`import/fhir/Questionnaires/blocks/<category>/<id>.json`, registered in
`blocks/blocks.json` and coding-aligned via `blocks/coding-registry.json`.
Blocks are the discussion vehicles with the pathologist and the blueprints for
composed Case Description forms.

## Notes

- File names containing brackets (`Hematology [C42-C42] ...md`) must be read
  with `Get-Content -LiteralPath` — the brackets are glob characters.
- Cross-cutting design decisions and open questions live in `FEEDBACK.md`.
- Status of composed questionnaires is tracked in
  `import/fhir/Questionnaires/registry.json`.