# Notes — Symptoms general

Companion notes for `Symptoms general.md` (pathologist original stays untouched).
Branch: `MAIN` · Server: `http://basyssrvdock1:8082` · Started: 2026-08-25

## Structure

Flat checklist of 20 symptoms, `yes/no`, region-tagged where the pathologist
marked one. `Pain`, `Swelling`, `Cough`, `Uterine bleeding` carry
"if yes, duration" (days/weeks/months[/years]).

## Questions for Jundt

- [x] Answers are Yes/No only (boolean, empty = unanswered) — D1.
- [x] Duration = quantity with selectable units (d/wk/mo/a) — D2.
- [x] "Other symptoms" gating + single-line reveal applied per composed form (D3/D7);
  the block itself stays a flat, source-faithful list.
- [ ] Scope: source tags C40/C42/C77 etc. — confirm the form-level `icdOScope`
  (see FEEDBACK Q1).
- [ ] Uterine bleeding uses US-extension code; uterine discharge coded as
  "Vaginal discharge" — confirm (FEEDBACK Q3).

## SNOMED lookups (resolved, verified)

| Question | Code | Display |
|---|---|---|
| Pain | `22253000` | Pain |
| Night pain | `36163009` | Night pain |
| Swelling | `65124004` | Swelling |
| Night sweat | `42984000` | Night sweats |
| Weight loss | `89362005` | Weight loss |
| Fever | `386661006` | Fever |
| Pallor | `274643008` | Body pale |
| Bleeding tendency | `78596001` | Bleeding diathesis |
| Susceptibility to infection | `102463001` | Susceptibility to infections |
| Jaundice | `18165001` | Jaundice |
| Cough | `49727002` | Cough |
| Breathlessness | `267036007` | Dyspnea |
| Constipation | `14760008` | Constipation |
| Diarrhoea | `62315008` | Diarrhea |
| Melaena | `2901004` | Melena |
| Nipple discharge | `54302000` | Discharge from nipple |
| Uterine bleeding | `44991000119100` | Abnormal uterine bleeding (US ext.) |
| Uterine discharge | `271939006` | Vaginal discharge |
| Dysuria | `49650001` | Dysuria |
| Haematuria | `34436003` | Blood in urine |
| Duration (child) | `162442009` | Time symptom lasts |

Rejected: `255399007` (= "Congenital (qualifier value)") for duration.

## Refinements

- All symptoms `boolean`; duration children `quantity` + `questionnaire-unitOption`
  (UCUM `d`/`wk`/`mo`/`a`; uterine bleeding `d`/`wk`/`mo`), `enableWhen` parent = true.
- Canonical linkIds (`fever`, `weightloss`, …) shared with region blocks via
  `blocks/coding-registry.json` so coding stays aligned.

## Blocks

- `symptoms.general` — topography `[]`.
