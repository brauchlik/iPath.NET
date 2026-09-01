# SNOMED CT resolution log — jundt-hematology-lymph

Branch: `MAIN` · Server: `http://basyssrvdock1:8082` · Date: 2026-08-18

**Endpoint note:** `/browser/MAIN/concepts?term=...` ignores the `term` filter on
this server (returns the full concept list). Term search is done via
`/browser/MAIN/descriptions?term=...&activeFilter=true&limit=N`.

## Labor — blood cell count (`lab.blood-count`)

| Question | Searched | Result count | Chosen code | FSN | Notes |
|---|---|---|---|---|---|
| Blood cell count available (gate) | `blood cell count` | – | `88308000` | Blood cell count (procedure) | gate boolean coded with the panel concept |
| Erythrocytes | `erythrocyte count`, `red blood cell count` | – | `14089001` | Red blood cell count (procedure) | FULLY_DEFINED; `Erythrocyte count` itself is not a PT — RBC count is the standard term |
| Granulocytes (group header) | `granulocyte count` | 13 | `118138007` | Granulocyte count (procedure) | group header coded; children carry subtype codes |
| Neutrophils | `neutrophil count` | – | `30630007` | Neutrophil count (procedure) | direct hit |
| Eosinophils | `eosinophil count` | – | `71960002` | Eosinophil count (procedure) | chose the `(procedure)` over `Finding of eosinophil count (finding)` `365601007` |
| Basophils | `basophil count` | – | `42351005` | Basophil count (procedure) | direct hit |
| Monocytes | `monocyte count` | – | `67776007` | Monocyte count (procedure) | chose `(procedure)` over `(finding)` `365631001` |
| Lymphocytes | `lymphocyte count` | – | `74765001` | Lymphocyte count (procedure) | direct hit |
| Thrombocytes | `platelet count` | – | `61928009` | Platelet count (procedure) | chose `(procedure)` over `(finding)` `365632008` |

## Notes

- For the differential items (eosinophils, monocytes, platelets) both a
  `(procedure)` and a `(finding)` concept exist; the `(procedure)` was chosen to
  match the "test performed" semantics of the questionnaire.

## v1.1 — units + LOINC secondary coding

SNOMED CT carries no units on these concepts; units verified on loinc.org and
added as `questionnaire-unitOption` (UCUM), SI first (= dropdown default), plus
a LOINC second coding:

| Question | LOINC | Unit options (UCUM) |
|---|---|---|
| Erythrocytes | `789-8` | `10*12/L` (default), `10*6/uL` |
| Neutrophils | `26499-4` | `10*9/L` (default), `10*3/uL` |
| Eosinophils | `711-2` | `10*9/L` (default), `10*3/uL` |
| Basophils | `704-7` | `10*9/L` (default), `10*3/uL` |
| Monocytes | `26484-6` | `10*9/L` (default), `10*3/uL` |
| Lymphocytes | `26474-7` | `10*9/L` (default), `10*3/uL` |
| Thrombocytes | `777-3` | `10*9/L` (default), `10*3/uL` |

Items changed `integer` → `quantity` (counts are decimal). See
`doc/jundt/FEEDBACK.md` D6, Q5, Q6.
