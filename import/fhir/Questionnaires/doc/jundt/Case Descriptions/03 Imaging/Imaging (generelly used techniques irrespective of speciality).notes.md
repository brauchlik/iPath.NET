# Notes — Imaging (generally used techniques, irrespective of speciality)

Companion notes for `Imaging (generelly used techniques irrespective of speciality).md`
(pathologist original stays untouched; "generelly" typo normalized in titles only).
Branch: `MAIN` · Server: `http://basyssrvdock1:8082` · Started: 2026-08-18 · Updated: 2026-08-26

## Structure

```
General (7 techniques)   boolean
└── Other                string
Special procedures Jaws (Mandible C41.1 / Maxilla C41.0)
├── Panoramic view       boolean
└── Cone-beam CT (CBCT)  boolean
Special procedures Breast
└── Mammography          boolean
```

## Decisions & Questions for Jundt

- [x] Are image files uploaded in the FHIR Questionnaire or in iPath? → **Images and reports are uploaded directly in the iPath system natively. No attachment inputs exist in the FHIR Questionnaire blocks.** (answered 2026-08-26 review).
- [ ] `PET MRI` and `PET/CT MRI` have no clean SNOMED concept — stay plain text?
- [ ] Breast topography `C50` is assumed (source does not state it) — confirm.

## SNOMED lookups (resolved, verified)

| Question | Code | Display |
|---|---|---|
| Conventional X-rays (2 planes) | `168537006` | Plain X-ray |
| Sonography | `16310003` | Ultrasonography |
| CT scan | `77477000` | Computed tomography |
| MRI | `113091000` | Magnetic resonance imaging |
| PET CT | `450436003` | Positron emission tomography with computed tomography |
| PET MRI | — | no clean concept (plain text) |
| PET/CT MRI | — | no clean concept (plain text) |
| Panoramic view | `89846007` | Orthopantogram |
| Cone-beam CT | `717193008` | Cone beam CT |
| Mammography | `71651007` | Mammography |

## Refinements

- Imaging items are clean `boolean` questions indicating whether the procedure was performed.
- All inline `attachment` upload inputs removed per iPath platform architecture.

## Blocks

- `imaging.general` — topography `[]`.
- `imaging.jaw` — topography `["C41.0", "C41.1"]`.
- `imaging.breast` — topography `["C50"]` (assumed, see Q).
