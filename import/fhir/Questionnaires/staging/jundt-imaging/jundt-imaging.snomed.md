# SNOMED CT resolution log — jundt-imaging

Branch: `MAIN` · Server: `http://basyssrvdock1:8082` · Date: 2026-08-18

**Endpoint note:** `/browser/MAIN/concepts?term=...` ignores the `term` filter on
this server (returns the full concept list). Term search is done via
`/browser/MAIN/descriptions?term=...&activeFilter=true&limit=N`.

## General imaging modalities (`imaging.general`)

| Question | Searched | Result count | Chosen code | FSN | Notes |
|---|---|---|---|---|---|
| Conventional X-rays (2 planes) | `x-ray` | 31 | `168537006` | Plain X-ray (procedure) | top match; FULLY_DEFINED |
| Sonography | `ultrasonography` | – | `16310003` | Ultrasonography (procedure) | direct hit |
| CT scan | `computed tomography` | – | `77477000` | Computed tomography (procedure) | direct hit; FULLY_DEFINED |
| MRI | `magnetic resonance imaging` | – | `113091000` | Magnetic resonance imaging (procedure) | direct hit |
| PET CT | `positron emission tomography computed tomography` | 100 | `450436003` | Positron emission tomography with computed tomography (procedure) | combined-modality procedure exists |
| PET MRI | `positron emission tomography magnetic resonance imaging` | 0 | — (plain text) | — | no combined-modality procedure in International edition; only `PET/MRI system` (physical object) `462824008` — not a procedure. Keep as plain text. **Review by pathologist.** |
| PET/CT MRI | `positron emission tomography` variants | 0 | — (plain text) | — | triple-modality combination; no concept. Keep as plain text. **Review by pathologist.** |
| Other | — | — | — | — | free text, no coding |

## Special imaging procedures — Jaws (`imaging.jaw`)

| Question | Searched | Result count | Chosen code | FSN | Notes |
|---|---|---|---|---|---|
| Panoramic view | `panoramic x-ray`, `panoramic` | 31 | `89846007` | Orthopantogram (procedure) | classic OPG concept; PRIMITIVE |
| Cone-beam computed tomography (CBCT) | `cone beam computed tomography` | 118 | `717193008` | Cone beam computed tomography (procedure) | generic CBCT; FULLY_DEFINED |

## Special imaging procedures — Breast (`imaging.breast`)

| Question | Searched | Result count | Chosen code | FSN | Notes |
|---|---|---|---|---|---|
| Mammography | `mammography` | 257 | `71651007` | Mammography (procedure) | FULLY_DEFINED |

## For pathologist review

- `PET MRI` and `PET/CT MRI` were left uncoded: the International edition has no
  combined PET/MRI (or PET/CT/MRI) procedure concept, only the scanner
  (`PET/MRI system`, `462824008`). If a local extension code is desired, propose
  one — otherwise they stay as plain-text options.
- `Panoramic view` → `Orthopantogram` (`89846007`). Matches the intent (OPG of
  the jaws). Alternative term `Panoramic dental x-ray` is not a procedure concept
  in this edition.
