# SNOMED CT resolution log — jundt-hematology-lymph-symptoms

Branch: `MAIN` · Server: `http://basyssrvdock1:8082` · Date: 2026-08-18

**Endpoint note:** term search via `/browser/MAIN/descriptions?term=...&activeFilter=true&limit=N`;
concept verification via `/browser/MAIN/concepts/{id}`.

This example is a draft variant of `jundt-hematology-lymph` with an appended
symptoms section. The lab codes are identical to the base form
(see `jundt-hematology-lymph.snomed.md`). The symptoms subsection codes are all
shared with the standalone symptoms form (see `jundt-symptoms.snomed.md`).

## v1.1 changes (per pathologist feedback)

- All symptom answers are `boolean` (Yes/No; empty = unanswered).
- Durations changed from `choice` string options to `quantity` items with a
  selectable unit (`questionnaire-unitOption`, UCUM codes `d` / `wk` / `mo` / `a`).
  Uterine bleeding keeps only `d` / `wk` / `mo` (the source lists no years).
- New gating question `sym.other` ("Other symptoms present") reveals the
  region-specific symptoms via `enableWhen`.
- Scope: "General" group = symptom set general for the C40/C77 scope of this
  form. Region-tagged symptoms are gated behind the "Other symptoms" question.
  `icdOScope` stays `["C42", "C77"]` per the source title.

## New SNOMED concept (v1.1)

| Question | Chosen code | Display | Notes |
|---|---|---|---|
| Duration (all four duration items) | `162442009` | Time symptom lasts | Verified via `snomed_get_by_code`; PT "Time symptom lasts", synonym "Duration of symptom". The initially suggested `255399007` resolves to "Congenital (qualifier value)" — rejected. |

All symptom codes are unchanged from v1.0: Pain `22253000`, Night pain `36163009`,
Swelling `65124004`, Night sweat `42984000`, Weight loss `89362005`, Fever
`386661006`, Breathlessness `267036007`, Pallor `274643008`, Bleeding tendency
`78596001`, Susceptibility to infection `102463001`, Jaundice `18165001`, Cough
`49727002`, Constipation `14760008`, Diarrhoea `62315008`, Melaena `2901004`,
Nipple discharge `54302000`, Uterine bleeding `44991000119100` (US extension code),
Uterine discharge `271939006` (coded as "Vaginal discharge"), Dysuria `49650001`,
Haematuria `34436003`.

## v1.2 changes

- Lab section: same unit/LOINC update as the base form v1.1 (`integer` →
  `quantity`, `questionnaire-unitOption` SI-first, LOINC second coding) — see
  `jundt-hematology-lymph.snomed.md` v1.1 table.
- "Other symptoms" now renders as a single line: the 11 region-specific items
  are nested directly under the `sym.other` gate boolean (each with
  `enableWhen`); the separate `sym.other-group` header group was removed.

## For pathologist review

- **Scope note:** the source title says "Hematology [C42-C42] and Lymph nodes
  [C77]" while the feedback describes the symptoms section as "specific to
  C40/C77". Confirm whether the form scope is `C42/C77` (source title, current
  `icdOScope`) or `C40/C77` (feedback) before assigning the BodySiteFilter.
- Duration items are coded `162442009` "Time symptom lasts" — confirm the
  concept fits the "how long present" intent.
- Blood-count items are absolute counts with units; confirm counts vs `%`
  (FEEDBACK.md Q5) and the SI dropdown default (Q6).
