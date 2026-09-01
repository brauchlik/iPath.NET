# jundt — design feedback log

Design decisions, open questions, and flagged issues captured while converting
the jundt source docs into FHIR R4 Questionnaires. This file is the shared memory
between the pathologist (Jundt) and the questionnaire builder.

## Decisions (answered)

### D1 — Symptoms answers are Yes/No only
`boolean` item type, empty = unanswered. No explicit "No" answer option needed.

### D2 — Duration is a quantity with selectable units
Duration items are `type: quantity` with `questionnaire-unitOption` extensions
(UCUM: `d` days, `wk` weeks, `mo` months, `a` years). The response is a
UCUM-coded `Quantity`. Uterine bleeding duration only offers `d`/`wk`/`mo`
(source lists no years).
Rationale: `unit` item type is unsupported in LHC-Forms 38.7.2;
`unitOption`/`questionnaire-unit` are supported and produce a computable Quantity.

### D3 — "Other symptoms" gating question
A boolean "Other symptoms present" question reveals the rest of the form via
`enableWhen`. Region-specific symptoms (e.g. Cough, Uterine bleeding) sit behind
this gate so the common symptoms stay visible.

### D4 — "General" means general for this form's scope
The "General symptoms" group covers the symptom set general for the C40/C77
scope of the form. Region-tagged symptoms are gated behind "Other symptoms".

### D5 — Feedback is stored here, not duplicated
Design decisions live in this file; `README.md` is the index of source docs →
questionnaires; the skill file only points here.

### D6 — Blood-count values carry a unit (quantity + dropdown, SI default)
SNOMED CT concepts for the counts carry no unit; LOINC is the unit source
(verified on loinc.org): Erythrocytes `10*12/L` ≡ `10*6/uL` (LOINC 789-8);
Neutrophils/Eosinophils/Basophils/Monocytes/Lymphocytes/Thrombocytes
`10*9/L` ≡ `10*3/uL` (LOINC 26499-4, 711-2, 704-7, 26484-6, 26474-7, 777-3).
The SI variant (Swiss eCH standard) is listed first = dropdown default.
Items changed `integer` → `quantity` (counts are decimal: RBC 4.8, Eos 0.05).
LOINC codes added as second `item.code` coding alongside SNOMED.

### D7 — "Other symptoms" renders as a single line
The gate boolean and the revealed list no longer produce two rows: the 11
region-specific symptoms are nested directly under the `sym.other` boolean
(each with `enableWhen`), the intermediate group header was removed.

### D8 — Originals immutable, notes layer added
The pathologist's source markdown is never edited. Internal thinking lives in a
readable `<name>.notes.md` next to each source (Structure / Questions / SNOMED
lookups / Refinements / Blocks). FHIR is compiled only from settled notes.

### D9 — Blocks are minimal FHIR Questionnaires
Blocks (`blocks/<category>/<id>.json`) are standalone R4 Questionnaires the
pathologist can load in LHC Forms, and blueprints for composed forms. Organised
`01 Material / 02 Symptoms / 03 Imaging / 04 Lab`, registered in
`blocks/blocks.json`. Old staging vehicles are superseded by blocks.

### D10 — One canonical coding per question
Shared questions reuse a canonical linkId and must match
`blocks/coding-registry.json` (text/type/SNOMED/LOINC/units), enforced by the
validator. Composed forms dedupe identical questions (region/richer wins).

## Open questions (pending Jundt)

### Q1 — Form scope C42/C77 vs C40/C77
Source title: "Hematology [C42-C42] and Lymph nodes [C77]" → `icdOScope`
`["C42","C77"]`. Feedback D4 describes the symptoms scope as "C40/C77".
Confirm before assigning the iPath BodySiteFilter.

### Q2 — SNOMED duration concept
Duration items use `162442009` "Time symptom lasts" (PT). Confirm the concept
matches the "how long present" intent. (`255399007` was rejected: it resolves
to "Congenital (qualifier value)".)

### Q3 — Uterine bleeding / discharge codes
"Uterine bleeding" uses US-extension code `44991000119100` "Abnormal uterine
bleeding" (no International concept found). "Uterine discharge" is coded as
`271939006` "Vaginal discharge" — confirm this is intended.

### Q4 — Duration for gated symptoms
D2 applied to Pain, Swelling (General) and Cough, Uterine bleeding (gated),
exactly where the source lists durations. Confirm whether the revealed "Other"
symptoms (Cough, Uterine bleeding) should keep duration items.

### Q5 — Absolute counts vs percentages
The blood-count items are coded as absolute counts (SNOMED `...count`,
LOINC `[#/volume]`, units `10*9/L` / `10*12/L`). Labs also report the
differential as a percentage (`%`). Confirm the pathologist intends absolute
counts here (the source says "Type a Number" with no unit).

### Q6 — Unit dropdown default
D6 relies on LHC-Forms defaulting the unit dropdown to the first
`questionnaire-unitOption` (the SI variant). Verify in the NLM Form Builder;
if not, fall back to `initial` with a unit-only `valueQuantity`.

## Scope markers in symptom text
Symptoms carry their source ICD-O region in parentheses, e.g. "Night sweat
(esp. C77)". The markers are advisory text; the iPath BodySiteFilter is set
manually in the admin UI after import.