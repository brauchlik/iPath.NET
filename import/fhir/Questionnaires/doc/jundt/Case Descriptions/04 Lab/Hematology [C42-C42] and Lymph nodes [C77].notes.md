# Notes — Hematology [C42-C42] and Lymph nodes [C77]

Companion notes for `Hematology [C42-C42] and Lymph nodes [C77].md`
(pathologist original stays untouched).
Branch: `MAIN` · Server: `http://basyssrvdock1:8082` · Started: 2026-08-18 · Updated: 2026-08-26

## Structure

```
Blood cell count available   boolean (gate)
Erythrocytes                 quantity (10*6/uL | 10*12/L)
Granulocytes                 group
├── Neutrophils              quantity (% | 10*3/uL | 10*9/L)
├── Eosinophils              quantity (% | 10*3/uL | 10*9/L)
└── Basophils                quantity (% | 10*3/uL | 10*9/L)
Monocytes                    quantity (% | 10*3/uL | 10*9/L)
Lymphocytes                  quantity (% | 10*3/uL | 10*9/L)
Thrombocytes                 quantity (10*3/uL | 10*9/L)
```

## Decisions & Questions for Jundt

- [x] Should differential counts support percentage (`%`) and `uL` units? → **Yes. MicroLiter (`uL`) is clinician-friendly. Differential items (Neutrophils, Eosinophils, Basophils, Monocytes, Lymphocytes) support `%` (default), `10*3/uL`, and `10*9/L` with dual LOINC mappings (Absolute + Percentage).** (answered 2026-08-26 review).

## SNOMED + LOINC lookups (resolved, verified)

| Question | SNOMED | LOINC (Absolute / %) | Units (UCUM, uL / % first) |
|---|---|---|---|
| Blood cell count available (gate) | `88308000` | — | — |
| Erythrocytes | `14089001` | `789-8` | `10*6/uL`, `10*12/L` |
| Granulocytes (group) | `118138007` | — | — |
| Neutrophils | `30630007` | `26499-4` / `770-8` | `%`, `10*3/uL`, `10*9/L` |
| Eosinophils | `71960002` | `711-2` / `713-8` | `%`, `10*3/uL`, `10*9/L` |
| Basophils | `42351005` | `704-7` / `706-2` | `%`, `10*3/uL`, `10*9/L` |
| Monocytes | `67776007` | `26484-6` / `5905-5` | `%`, `10*3/uL`, `10*9/L` |
| Lymphocytes | `74765001` | `26474-7` / `736-9` | `%`, `10*3/uL`, `10*9/L` |
| Thrombocytes | `61928009` | `777-3` | `10*3/uL`, `10*9/L` |

## Refinements

- `quantity` + `questionnaire-unitOption` with `%` and `10*3/uL` prioritized for clinician usability.
- Dual LOINC codes added for WBC differential items to support both relative percentage and absolute volume count reporting.

## Blocks

- `lab.blood-count` — topography `["C42", "C77"]`.
