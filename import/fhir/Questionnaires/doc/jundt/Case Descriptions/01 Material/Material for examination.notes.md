# Notes — Material for examination

Companion notes for `Material for examination.md` (pathologist original stays untouched).
Branch: `MAIN` · Server: `http://basyssrvdock1:8082` · Started: 2026-08-25 · Updated: 2026-08-26

## Structure

```
Cytology Yes/No                      (gate, boolean)
├── Pap smear                        (option)
├── Smear cytology (eg Bronchus)     (option)
├── Sputum                           (option)
├── Exfoliative cytology             (option)
│   ├── Liquor                       (sub-option)
│   ├── Pleural effusion             (sub-option)
│   ├── Ascites                      (sub-option)
│   ├── Urine                        (sub-option)
│   └── Lavage                       (sub-option)
└── Fine needle aspiration           (option)

Histology Yes/No                     (gate, boolean)
├── Core biopsy                      (option)
├── Open biopsy                      (option)
├── Excisional biopsy                (option)
└── Resection                        (option)
```

## Decisions & Questions for Jundt

- [x] How are the indented sub-types answered? → **multi-select when the gate is Yes** (answered 2026-08-25).
- [x] Can Cytology and Histology both be Yes at the same time? → **Yes means Yes. Both gates are independent top-level questions. A case can have both Cytology and Histology materials submitted simultaneously.** (answered 2026-08-26 review).
- [x] Is "No data" option needed inside multi-select choice lists? → **No. "No data" in multi-choice lists makes no sense because leaving items unselected or leaving the gate unanswered already represents unanswered/no data.** (answered 2026-08-26 review).
- [ ] Is the exfoliative cytology sub-list also multi-select? (assumed yes)
- [ ] Gates uncoded (no active SNOMED procedure concept found) — acceptable?

## SNOMED lookups

| Term | Result | Decision |
|---|---|---|
| Cytology (gate) | `702666009` = "Cytology technique (qualifier value)" | qualifier, not a procedure — leave gate **uncoded** |
| Histology (gate) | no active concept | leave **uncoded** |
| Pap smear | `119252009` Papanicolaou smear test **inactive** | option stays **plain text** |
| Smear cytology | no active concept | **plain text** |
| Sputum | `45710003` Sputum | recorded for future item-level coding |
| Exfoliative cytology | no active concept | **plain text** |
| Fine needle aspiration | `257759008` Fine needle aspiration biopsy | recorded |
| Core biopsy | `9911007` Core needle biopsy | recorded |
| Open biopsy | `119283008` Open biopsy | recorded |
| Excisional biopsy | `8889005` Excisional biopsy | recorded |
| Resection | `65801008` Excision (syn "Resection") | recorded |

## Refinements

- Gates: `Cytology` and `Histology` are flat top-level `boolean` items.
- Sub-types: `choice` + `repeats: true` (multi-select), with `enableWhen` referencing the respective gate = `true`.
- `"No data"` option removed from multi-select sub-type choice lists.

## Blocks

- `material.examination` — topography `[]` (common to all regions).
