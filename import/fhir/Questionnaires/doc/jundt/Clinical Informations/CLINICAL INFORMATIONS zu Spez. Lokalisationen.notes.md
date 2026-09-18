# Notes — CLINICAL INFORMATIONS zu Spez. Lokalisationen

Companion notes for `CLINICAL INFORMATIONS zu Spez. Lokalisationen.md`, the readable
transcription of the immutable `CLINICAL INFORMATIONS zu Spez. Lokalisationen.odt`.
Originals are never edited; this file holds all internal thinking.
Branch: `MAIN` · Server: `http://basyssrvdock1:8082` · Started: 2026-09-17

## Structure

Seven region tables, 4-column source layout (`Question | Answers | Conditional | Detail`),
~60 questions. Sections and their ICD-O scope:

| # | Section (verbatim) | Scope |
|---|---|---|
| 1 | Esophagus, Stomach | C15–C16 |
| 2 | Intestine | C17–C21 |
| 3 | Liver | C22 |
| 4 | Hematopoetic and Reticuloendothelial System | C42 |
| 5 | Lymph nodes | C77 |
| 6 | Breast | C50 |
| 7 | Female genital Organs | C60–C63 |

Answer vocabulary in use: `yes/no/unknown`, `Available: yes/no`, `Yes/No`, `no/yes`,
`hard/smooth/unknown`, `Irregular/regular/unknown`.

Two gates: `Blood cell count — Available: yes/no` (C42, reveals 9 counts) and
`Accidental finding — Yes/No; if No:` (C77, reveals 8 symptoms).

## Content Classification

All items are **Case Description** (clinical information captured at intake), *except:*

- **Lab** (`04 Lab`): `Liver Enzymes elevated` → GPT/ALT, GOT/AST, Gamma GT (U/L), and the
  whole C42 blood-count table (`lab.hematology` already exists).
- **Cross-reference**: C77 `Blood cell count available … (see C42)` — the C77 table itself
  points at the C42 table. One shared lab block, not two.
- **Nothing here is Final Assessment.** No margin status, no staging, no grading.
- **Borderline**: `Microcalcifications on imaging` (C50) is imaging-report content but is
  listed inside the clinical-information table (see Q8).

Wizard-managed and therefore excluded: nothing in this doc (no age/gender/ICD-O/institute).

## Reuse mapping (verified against `blocks/coding-registry.json`)

**~34 of ~60 items are already canonical.** These carry no work.

| Source item | Canonical linkId | SCT | Note |
|---|---|---|---|
| Pain (C15–C16 duration) | `pain` / `pain.duration` | 22253000 / 162442009 | see Q9 |
| Pain (C17–C21 + region grid) | `pain.abdominal` / `.location` / `.duration` | 21522001 | registry grid has 6 r/l options, source adds `No data` |
| Dysphagia | `dysphagia` | 40739000 | |
| Painful swallowing | `painfulswallowing` | 30233002 | |
| Regurgitation | `regurgitation` | 78104003 | |
| Hematemesis | `vomiting.blood` | 8765009 | |
| Weight loss | `weightloss` (+`weightloss.kg`) | 89362005 | digesting source had kg |
| Melaena | `melaena` | 2901004 | |
| Bloody stool | `bloodystool` | 405729008 | |
| Jaundice | `jaundice` | 18165001 | |
| Easy bruising | `easybruising` | 424131007 | |
| Blood cell count available | `available` | 88308000 | |
| Erythrocytes / Granulocytes / Neutrophils / Eosinophils / Basophils / Monocytes / Lymphocytes / Thrombocytes | same ids | 14089001, 118138007, 30630007, 71960002, 42351005, 67776007, 74765001, 61928009 | all with LOINC + units (D6) |
| Swelling (+duration) | `swelling` / `swelling.duration` | 65124004 / 162442009 | |
| Night sweat | `nightsweat` | 42984000 | |
| Fever | `fever` | 386661006 | |
| Pallor | `pallor` | 274643008 | |
| Bleeding tendency | `bleedingtendency` | 78596001 | |
| Susceptibility to infection | `susceptibility` | 102463001 | |
| Breast lump (+since) | `breast.lump` | 274647000 | |
| Consistency / Border | `breast.lump.consistency` / `.border` | — | intentionally uncoded choice |
| Nipple discharge | `nippledischarge` | 54302000 | |
| Nipple retracted | `breast.retraction` | 69085004 | |
| Breast-skin discolored | `breast.skindiscoloration` | 402773007 | |
| Pregnancy / week / previous / last delivery | `pregnancy*` | 77386006, 1156895004, 161732006, 161714006 | |
| Menopause / postmenopausal bleeding / regular cycle | `menopause*` | 426979002, 289530006, 289947009 | |
| Lower abdominal pain / persistent swelling | `pain.lowerabdominal` / `swelling.lowerabdominal` | 3006004 / 285376008 | |
| Vaginal bleeding / discharge | `vaginalbleeding` / `vaginaldischarge` | 249021008 / 271939006 | |

**Net effect: the entire C42 table is already built** (`blocks/04 Lab/lab.hematology.json`).

## SNOMED lookups — 2026-09-17

**Server state.** Snowstorm (`http://basyssrvdock1:8082`) is in a degraded state: by-code
`$lookup` answered (21 s → `Pain`, SNOMED CT release 2025-04-01) while every search
endpoint hung (`/browser/MAIN/descriptions` > 150 s, `/fhir/ValueSet/$expand` > 90 s,
`/actuator/health` > 45 s). Later even the by-code path timed out (> 90 s).

**Workaround used.** Lookups were run against a hosted FHIR terminology server via the
MCP `remote` backend (`SNOMED_BACKEND=remote`, `https://r4.ontoserver.csiro.au/fhir`).

> **Edition caveat — that server carries SNOMED CT *Australian* edition.** International
> concept ids match the pair we ship against (spot-checks: `840539006` → COVID-19,
> `22253000` → Pain, `162442009` → Time symptom lasts). **Re-verify every code below
> against the local International server once Snowstorm search is repaired.**

### Resolved — new to this document

| Source item | SCT | Display (PT) | FSN semantic tag |
|---|---|---|---|
| Liver Enzymes elevated | `707724006` | Elevated liver enzymes level | (finding) |
| GPT/ALT (U/L) | `390961000` | Plasma alanine aminotransferase level | (procedure) |
| GOT/AST (U/L) | `373679006` | Aspartate transaminase level | (finding) |
| Gamma GT (U/L) | `69480007` | Gamma glutamyl transferase measurement | (procedure) |
| Microcalcifications on imaging | `697944008` | Mammographic calcification of breast | (disorder) |
| Irregular periods | `80182007` | Irregular menstruation | (finding) |
| Heavy menstrual bleeding / Menorrhagia | `386692008` | Heavy menstrual bleeding | (finding) |
| Abnormal bleeding / Spotting | `237130006` | Intermenstrual bleeding | (finding) |
| Missed periods / Amenorrhea | `14302001` | Amenorrhoea | (finding) |
| Painful periods / Dysmenorrhea | `266599000` | Dysmenorrhoea | (disorder) |

### Resolved — C77 Localization and Organ involved

All are `(body structure)`:

| Source option | SCT | FSN |
|---|---|---|
| head, face and Neck | `81105003` | Cervical lymph node structure |
| Intrathoracic | `62683002` | Mediastinal lymph node structure |
| Intra-abdominal | `818991007` | Structure of abdominal lymph node |
| axilla or arm | `68171009` | Structure of axillary lymph node |
| inguinal region or Leg | `8928004` | Inguinal lymph node structure |
| Pelvic lymph nodes | `54268001` | Pelvic lymph node structure |
| Lymph node, NOS | `59441001` | Structure of lymph node |
| Organ involved: Spleen | `78961009` | Splenic structure |
| Organ involved: Liver | `10200004` | Liver structure |
| Organ involved: Bone marrow | `14016003` | Bone marrow structure |

### Deliberately left uncoded

| Source item | Why |
|---|---|
| Accidental finding (C77 gate) | no generic concept; `incidentaloma` results are organ-specific (`97881000119105`, `83191000119105`). Keep plain boolean or a local `http://ipath.local` code |
| Previous steroid treatment | no `(situation)` analogue to the existing `radiation.history` (`416237000`). Only procedure-flavoured candidates: `1290388005` Systemic corticosteroid therapy, `788751009` Corticosteroid and/or corticosteroid derivative therapy, `12240661000119103` Long term systemic steroid user (US extension). Keep plain boolean — see Q4 |
| Distribution: above/below the diaphragm | the options are qualifiers, not concepts; `5798000` Structure of diaphragm is the only structure. Keep uncoded choice |
| what side (right, middle, left) | laterality qualifier, not a concept. Keep uncoded choice |
| Breast: increasing in size since | a date, not a concept (see Q2) |
| Breast Consistency / Border | already intentionally uncoded in `coding-registry.json` |
| `other` (C17–C21) | free text |

### Rejected candidates (recorded)

| Candidate | Rejected because |
|---|---|
| `389586005`, `390318006` | inactive duplicates of the ALT level; `390961000` is the active one |
| `409673008` Alanine aminotransferase above reference range | captures the abnormal *finding*, not the measured level |
| `309587003` Calcification of breast | generic (disorder); `697944008` is the mammographic variant matching "on imaging" |
| `60153001` Gamma-glutamyltransferase | (substance); the measurement is `69480007` |
| `166603001` Liver function tests abnormal | superseded by the more specific `707724006` |
| `274647000` (registry `breast.lump`) | returns 404 — see defect below |

### Registry defect found (outside this doc's scope)

`coding-registry.json` codes `breast.lump` as `274647000`, but that SCTID **404s** on the
hosted server. The valid active concept is **`89164003` Breast lump (finding)**. Blocks
carrying `274647000` (`symptoms.json`, `symptoms.breast-female.json`) are therefore
suspect. Confirm against the local International server before correcting — this touches
already-`coded` blocks.

## Questions for Jundt

- [ ] **Q1 — "Accidental finding: Yes/No; if No:".** The 8 revealed symptoms sit under
      *if No*. Read literally, symptoms are asked only when it is *not* an accidental
      finding. Intended? Or should the reveal be on *Yes*?
- [ ] **Q2 — `Duration: DD MM YY`** (C15–C16 and C17–C21 Pain). Reads as a *date*; our D2
      convention makes duration a `quantity` in d/wk/mo/a. Which is it? (Source detail
      column is `DD MM YY`, while other sections say `(d, m, y, no data)`.)
- [ ] **Q3 — duplicate `Erythrocytes (specify)`** in the C42 table (rows 1 and 9). Typo?
      If a second distinct parameter was intended — Hb, haematocrit, or total granulocytes?
- [ ] **Q4 — Liver enzymes** — confirm they belong in the Lab form, and that `U/L` is the
      only unit needed.
- [ ] **Q5 — `Microcalcifications on imaging`** — clinical information here, or the
      `imaging` block?
- [ ] **Q6 — Breast lump children** — `since when (d-m-y)` vs `increasing in size since
      (d-m-y)`: two separate date questions, and does "increasing in size" need a
      yes/no gate first?
- [ ] **Q7 — Menopause branch nesting** (C60–C63): `Menopause → if no → Regular menstrual
      cycle → if no → Irregular periods / Menorrhagia / Spotting / Amenorrhea /
      Dysmenorrhea`. Confirm the last group is a **multi-select** and that it is reached
      only when the cycle is *not* regular.
- [ ] **Q8 — Localization / Organ involved** are marked `(more than one allowed)` →
      multi-select. Confirm.
- [ ] **Q9 — Esophagus/Stomach `Pain`** — general `pain` (22253000) or abdominal
      `pain.abdominal` (21522001)? Currently pain.abdominal is used for C15–C26.
- [ ] **Q10 — Pain duration in C15–C26** uses the 6-quadrant grid in the old digestive
      doc and a 3-region grid here (`Upper/Middle/Lower abdomen (r/l) + No data`).
      Which grid wins?
- [ ] **Q11 — section 7 heading contradicts its ICD-O range: "Female genital Organs
      C60 - C63".** In ICD-O-3 `C51–C58` is *female* genital organs and `C60–C63` is
      *male* — confirmed against Jundt's own filter list in
      `originals/Clinical data_Details.docx` ("Female genital organs [C51-C58]",
      "Male genital organs [C60-C63]"), and the existing `symptoms.breast-female` block
      is scoped `["C50","C51-C58"]`. The section *content* (pregnancy, menopause,
      menstrual irregularities, vaginal bleeding/discharge) is unambiguously female, so
      the range looks like a typo for `C51–C58`. **Confirm before assigning the
      BodySiteFilter** — as written it would attach female questions to male-genital cases.

## Refinements

- Reuse-first: linkIds for every mapped item come from `coding-registry.json` unchanged
  (D10 one canonical coding). Only new items enter the registry after SNOMED resolution.
- Category, block ids, and the `02 Symptoms` replacement are **deliberately deferred**
  until after this review (agreed 2026-09-17).

## Blocks (planned, not compiled)

Deferred until review. Intended shape:

```
blocks/02 Clinical Information/
  clinical.json                  master list
  clinical.esophagus-stomach.json   C15-C16
  clinical.intestine.json           C17-C21
  clinical.liver.json               C22
  clinical.hematopoietic.json       C42   → overlaps existing lab.hematology
  clinical.lymph-nodes.json         C77
  clinical.breast.json              C50   → overlaps symptoms.breast-female
  clinical.female-genital.json      C60-C63 → overlaps symptoms.breast-female
```

Known overlaps to resolve at replacement time: `lab.hematology` (C42), and
`symptoms.breast-female` (C50 + C60–C63).
