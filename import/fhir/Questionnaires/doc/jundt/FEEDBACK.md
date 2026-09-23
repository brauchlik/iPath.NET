# jundt — design feedback log

Design decisions, open questions, and flagged issues captured while converting
the jundt source docs into FHIR R4 Questionnaires. This file is the shared memory
between the pathologist (Jundt) and the questionnaire builder.

Method behind all of this: `.opencode/skills/questionnaire-builder/reference/specialist-elicitation.md`.
Dated evidence: `LESSONS.md` in this folder.

## Decisions (answered)

### D1 — Symptoms answers are Yes/No only — **superseded 2026-09-10**
`boolean` item type, empty = unanswered. No explicit "No" answer option needed.

**Superseded:** on 2026-09-10 (`Form Suggestions — Questions for Dr. Jundt.md` §3) we adopted
Jundt's 3-state format — `unknown` is genuinely different from `no` — and agreed to update all
symptom blocks. **That change was never applied**, so every symptom item is still `boolean`,
which renders *Yes / No / Not Answered*. A boolean cannot express "unknown"; the fix is a
3-state `choice` (`Yes` / `No` / `Unknown`). See open item N1.

### D2 — Duration is a quantity with selectable units
Duration items are `type: quantity` with `questionnaire-unitOption` extensions
(UCUM: `d` days, `wk` weeks, `mo` months, `a` years). The response is a
UCUM-coded `Quantity`. Uterine bleeding duration only offers `d`/`wk`/`mo`
(source lists no years).
Rationale: `unit` item type is unsupported in LHC-Forms 38.7.2;
`unitOption`/`questionnaire-unit` are supported and produce a computable Quantity.

### D3 — "Other symptoms" gating question — **superseded 2026-09-21**
A boolean "Other symptoms present" question reveals the rest of the form via
`enableWhen`. Region-specific symptoms (e.g. Cough, Uterine bleeding) sit behind
this gate so the common symptoms stay visible.

**Superseded:** the general symptom list and its gate were rejected by Jundt
(annotated PDF, 2026-09-21). See D14.

### D4 — "General" means general for this form's scope — **superseded 2026-09-21**
The "General symptoms" group covers the symptom set general for the C40/C77
scope of the form. Region-tagged symptoms are gated behind "Other symptoms".

**Superseded** by D14.

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

### D7 — "Other symptoms" renders as a single line — **superseded 2026-09-21**
The gate boolean and the revealed list no longer produce two rows: the 11
region-specific symptoms are nested directly under the `sym.other` boolean
(each with `enableWhen`), the intermediate group header was removed.

**Superseded** by D14.

### D8 — Originals immutable, notes layer added
The pathologist's source markdown is never edited. Internal thinking lives in a
readable `<name>.notes.md` next to each source (Structure / Questions / SNOMED
lookups / Refinements / Blocks). FHIR is compiled only from settled notes.

### D9 — Blocks are minimal FHIR Questionnaires
Blocks (`blocks/<category>/<id>.json`) are standalone R4 Questionnaires the
pathologist can load in LHC Forms, and blueprints for composed forms. Organised
`01 Material / 02 Symptoms / 03 Imaging / 04 Lab`, registered in
`blocks/blocks.json`. Old staging vehicles are superseded by blocks.

*(Category naming updated by D16: the symptom family becomes `clinical`.)*

### D10 — One canonical coding per question
Shared questions reuse a canonical linkId and must match
`blocks/coding-registry.json` (text/type/SNOMED/LOINC/units), enforced by the
validator. Composed forms dedupe identical questions (region/richer wins).

## Decisions 2026-09-21 (v2 review)

### D11 — "Previous radiation therapy" is clinical information, not assessment
It sat in the Diagnostic block only because Jundt wrote it *inside* the "Soft tissue Tumours"
section of his original docx — section position is not a semantic classification. It is
history the **requester** supplies, so it is re-filed as clinical information. The code was
also wrong: `416237000` resolves to *"Procedure not done (situation)"*; the correct concept is
**`429479009`** (`H/O: radiation therapy`).

### D12 — Previous-treatment items carry a free-text detail
`Previous radiation therapy` and `Previous chemotherapy` (new, `161653008`) each carry a
single free-text sibling **"Time period and protocol"**, revealed by `enableWhen`
`answerString "Yes"`. One field, not two.

### D13 — A reveal bound to one option is a sibling, never a child
For a `choice` item the detail sits beside it with `enableWhen answerString`, per
`block-schema.md`. The renderer confirms it: `CaseDescriptionToTextService.ProcessChoiceItem`
never collects children, so a child would never render.

### D14 — No general symptom list
Rejected by Jundt 2026-09-21: *"Diese Liste ist nicht notwendig … die neue Zusammenstellung
führt nur die Symptome auf, die für die jeweilige C-Region sinnvoll sind."* Composition is
**region-specific only**, and each region keeps one free-text **"Other"** field. The
`symptoms` master block and `symptoms.digestive` / `.respiratory` / `.breast-female` are
deprecated; **the items survive**, re-cut into `clinical.*` region blocks.

### D15 — "Other" is a free-text field
Plain text, no yes/no gate — the gate in his table template is an artefact. `text`
(multi-line) preferred over `string`, applied consistently.

### D16 — Family naming
Block family is **`clinical`** (`clinical.lymph-nodes`, `clinical.breast`, …); item ids stay
region-neutral (`pain`, `fever`); composed-form group linkIds are explicit —
`material` / `clinical` / `lab` / `imaging`.

### D17 — Free text must reach the case-description preview
`CaseDescriptionToTextService` renders Boolean / Choice / OpenChoice / Quantity / Group only,
so a `string` answer is exported but dropped from the narrative. Fix: render `String`/`Text`,
**skip when empty, show when filled**.

### D18 — Staged forms retired
`jundt-case-hematology-lymph` and `jundt-case-digestive` retired; nothing is published yet.
The C77 and C49 samples become worked examples of the method, not staged artefacts.

### D19 — Coding status is recorded per item
Every registry entry carries `coded` / `uncoded-by-decision` / `no-concept-found`, plus a
`verified { edition, date, via }` stamp. Inherited codes are re-verified, never trusted.

### D20 — Answer vocabulary: yes / no / unknown
Confirmed with Jundt by phone, 2026-09-21. Symptom and history questions are a **3-state
`choice`** — `Yes` / `No` / `Unknown` — three explicit answers; the reason for "unknown" is
**not** captured. Structural gates stay `boolean` (material: cytology/histology; lab
availability; imaging modality), because those are binary facts rather than clinical states.

Applied 2026-09-21: 47 registry items converted from `boolean` to `choice`; the C77 and C49
samples rebuilt. Supersedes D1 — which had itself been superseded on 2026-09-10 and never
applied (see `LESSONS.md`).

A detail revealed by one option is a **flat sibling** with `enableWhen answerString "Yes"`,
never a child: the case-description renderer does not collect children of a `choice` (D13).

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

### Q7 — "Other" as free text, or structured?
Plain free text is our recommendation (his own words: *"nur als Freitext-Feld"*), and a
catch-all that requires coding is not a catch-all. Ask only whether he wants the detail
**period and protocol** as one field or two.

## Open (ours — no Jundt input needed)

### N1 — 3-state symptoms — **done 2026-09-21**
See D20. The deprecated `symptoms*` blocks still contain `boolean` items; they are retired and
will be re-cut as `clinical.*` region blocks during the inventory pass.

### N2 — `tier` per item (`core` / `standard` / `extended`)
Undecided. Without it we cannot express "minimal form for a generalist group" vs
"extensive form for a specialist group" from a prompt.

### N3 — Community-level assignment
`QuestionnaireForCommunity` exists in the model but is not merged into case creation; the
assignment unit today is the **group**. A community such as the Arbeitsgemeinschaft
Knochentumor would want one form across many groups.

## Scope markers in symptom text
Symptoms carry their source ICD-O region in parentheses, e.g. "Night sweat
(esp. C77)". The markers are advisory text; the iPath BodySiteFilter is set
manually in the admin UI after import. With region-specific blocks (D14) the markers become
redundant and should be dropped as items are re-cut.
