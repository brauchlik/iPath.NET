# Review Notes — Case Description: Hematology and Lymph Nodes

**Source:** Dr. Jundt's form suggestion + meeting plan 2026-09-02
**Status:** staged (v2.0 — rebuilt from scratch)
**Topography:** C42 (Hematology), C77 (Lymph nodes)

## Content classification

| Content | Form Type | Reason |
|---|---|---|
| Submitter, journal number, age, gender, localization, suspected diagnosis | Wizard-managed | Handled by iPath wizard |
| Type of disease (inflammation, neoplasia, etc.) | Deferred | See open questions |
| Material (cytology, histology) | CaseDescription | Intake info |
| Previous steroid treatment | Deferred | See open questions |
| Accidental finding | Deferred | See open questions |
| Symptoms (pain, swelling, night sweat, etc.) | CaseDescription | Presenting complaint |
| Blood cell count | CaseDescription | Lab at intake |
| Hematology compartments (blood, bone marrow, spleen) | Deferred | See open questions |
| Lymph node mapping (location, organ involvement) | Deferred | See open questions |
| Imaging | CaseDescription | Not included in first pass (per meeting plan) |
| Extended lab (Hb, VitB12, Fe, MCV, etc.) | Deferred | See open questions |

## Composition summary

| Section | Block | linkId prefix | Items |
|---|---|---|---|
| Material | `material` | `mat.` | Cytology (bool + types), Histology (bool + types) |
| Symptoms | `symptoms` | `sym.` | 8 C77-relevant: pain (+duration), swelling (+duration), night sweat, weight loss (+kg), fever, pallor, bleeding tendency, susceptibility |
| Other Symptoms | `symptoms` | `sym.other.` | 23 non-C77 items behind "Other symptoms present?" gate |
| Blood cell count | `lab.hematology` | `lab.` | Available gate, erythrocytes, granulocytes (3 sub-counts), monocytes, lymphocytes, thrombocytes |

## What was applied

- **D1:** Boolean yes/no for all symptoms (not 3-state)
- **D3/D7:** "Other symptoms present?" gate reveals non-hematology symptoms
- **Composition rules:** linkId prefixing, enableWhen rewriting, no dedup needed (blocks are disjoint)
- **No imaging:** per meeting plan
- **No diagnostic:** Diagnostic Assessment is a separate `FinalAssessment` form type
- **No new inline sections:** only existing blocks
- **Section naming:** "Symptoms" (not "Symptoms for C77") — the whole form is C77-specific

## Open questions

1. **Type of disease** (inflammation, infectious, autoimmune, genetic, metabolic, hematologic, neoplasia) — Jundt wants this as a multi-select. Should we add it as a new section, or defer to a future block?
2. **Previous steroid treatment** (yes/no/unknown) — clinical history, belongs in CaseDescription. Add inline or defer?
3. **Accidental finding** (yes/no) — clinical context, belongs in CaseDescription. Add inline or defer?
4. **Hematology compartments** (blood, bone marrow, spleen, other) — clinical info at intake. Add inline or defer?
5. **Lymph node mapping** (location: cervical, axillary, inguinal, intrathoracic, intraabdominal, retroperitoneal; organ involvement: spleen, liver, bone marrow) — clinical info at intake. Add inline or defer?
6. **Extended lab** (Hb, VitB12, Fe, MCV, MCH, etc.) — lab at intake. Add inline or defer?
7. **Imaging** — Jundt's form includes general imaging (X-ray, US, CT, MRI, PET). Meeting plan says no imaging for hematology. Confirm?
