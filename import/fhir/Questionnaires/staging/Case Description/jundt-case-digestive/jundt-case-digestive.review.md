# Review Notes — Case Description: Digestive Organs

**Source:** Meeting plan 2026-09-02 + symptoms.digestive block
**Status:** staged (v2.0 — rebuilt from scratch)
**Topography:** C15-C26 (Digestive organs)

## Content classification

| Content | Form Type | Reason |
|---|---|---|
| Submitter, journal number, age, gender, localization, suspected diagnosis | Wizard-managed | Handled by iPath wizard |
| Material (cytology, histology) | CaseDescription | Intake info |
| Digestive symptoms (dysphagia, heartburn, abdominal pain, etc.) | CaseDescription | Presenting complaint |
| Other symptoms (pain, swelling, night sweat, etc.) | CaseDescription | Non-digestive symptoms at intake |
| Imaging (X-ray, US, CT, MRI, PET) | CaseDescription | If available at intake |
| Diagnostic assessment (margin status, tumor staging) | FinalAssessment | NOT CaseDescription |

## Composition summary

| Section | Block | linkId prefix | Items |
|---|---|---|---|
| Material | `material` | `mat.` | Cytology (bool + types), Histology (bool + types) |
| Symptoms | `symptoms.digestive` | `sym.` | 18 digestive-specific: dysphagia, heartburn, painful swallowing, regurgitation, dyspepsia, abdominal pain (+location, +duration), flatulence, weight loss (+kg), vomiting (+blood), constipation, diarrhoea, stool changes, melaena, bloody stool, fever, jaundice, easy bruising, other |
| Other Symptoms | `symptoms` | `sym.other.` | 21 non-digestive items behind "Other symptoms present?" gate |
| Imaging | `imaging` | `img.` | X-ray, sonography, CT, MRI, PET CT, PET MRI, PET/CT MRI, other |

## What was applied

- **D1:** Boolean yes/no for all symptoms (not 3-state)
- **D3/D7:** "Other symptoms present?" gate reveals non-digestive symptoms
- **Composition rules:** linkId prefixing, enableWhen rewriting, no dedup needed (blocks are disjoint)
- **No diagnostic:** Diagnostic Assessment is a separate `FinalAssessment` form type
- **No new inline sections:** only existing blocks
- **Section naming:** "Symptoms" (not "Symptoms for C15-C26") — the whole form is digestive-specific

## Open questions

1. **Awaiting Jundt's own form suggestion** for digestive organs — current form is a best-effort draft from the meeting plan
2. **Clinical info sections** (type of disease, steroid treatment) — should these be included in the digestive form?
3. **Imaging scope** — general imaging only confirmed? No organ-specific modalities (e.g. endoscopic ultrasound)?
