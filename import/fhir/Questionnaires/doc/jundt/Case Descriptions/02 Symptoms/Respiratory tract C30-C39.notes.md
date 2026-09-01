# Notes — Respiratory tract C30-C39

Companion notes for `Respiratory tract C30-C39.md` (pathologist original stays untouched).
Branch: `MAIN` · Server: `http://basyssrvdock1:8082` · Started: 2026-08-31

## Structure

```
Chronic cough (>3 months)        boolean
Shortness of breath              boolean
Dyspnea                          boolean
Coughing up blood (Hemoptysis)   boolean
Chronic chest pain               boolean
Smoker                           boolean
└── Duration (years)             quantity (a / years)  [enableWhen: Smoker = Yes]
Expectoration (sputum)           boolean
```

## Decisions & Questions for Jundt

- [ ] "Shortness of breath" vs "Dyspnea": In medical terminology and SNOMED CT, "Shortness of breath" is the preferred synonym for "Dyspnea" (`267036007`). Confirm whether to keep both as distinct items or merge them into a single question.
- [ ] Chronic cough: Distinguishes chronic cough (> 3 months) from general acute cough.
- [ ] Smoking history: Coded as boolean gate + duration in years. Confirm whether pack-years or simple duration (years) is preferred.

## SNOMED lookups

| Question / Term | Code | Display | Notes |
|---|---|---|---|
| Chronic cough (>3 months) | `68154008` | Chronic cough | Finding |
| Shortness of breath | `267036007` | Dyspnea | syn "Shortness of breath" |
| Dyspnea | `267036007` | Dyspnea | Same concept as Shortness of breath |
| Coughing up blood | `66857006` | Hemoptysis | Finding |
| Chronic chest pain | `82886004` | Chronic chest pain | Finding |
| Smoker | `77176002` | Smoker | Finding |
| Smoking duration | `160617001` | Number of years smoked | Observable entity |
| Expectoration (sputum) | `284523002` | Sputum production | Finding |

## Refinements

- All symptoms are `boolean` (Yes / No).
- `Smoker` has child `smoker.years` (`quantity` with unit `a` / years), enabled when `Smoker = true`.
- Overlap handling: Composed forms for thoracic/lung cases will include these respiratory-specific symptoms.

## Blocks

- `symptoms` — Master Symptoms Block (topography tag `["C30-C39"]`).
