# SNOMED CT resolution log — jundt-symptoms

Branch: `MAIN` · Server: `http://basyssrvdock1:8082` · Date: 2026-08-18

**Endpoint note:** term search via `/browser/MAIN/descriptions?term=...&activeFilter=true&limit=N`;
concept verification via `/browser/MAIN/concepts/{id}` (authoritative PT/FSN).
The `snomed_lookup` MCP tool (`ValueSet/$expand` over `<<404684003`,
`<<363787002` etc.) hangs on this server for large hierarchies, so it was only used
for rarer-code verification (`snomed_get_by_code`), which also failed for two
International codes that the browser API resolves fine.

## Symptoms (`sym.general`)

| Question | Searched | Result count | Chosen code | FSN | Notes |
|---|---|---|---|---|---|
| Pain | `pain` | 3 | `22253000` | Pain (finding) | general pain finding |
| Night pain | `night pain` | 3 | `36163009` | Night pain (finding) | C40 annotation |
| Swelling | `swelling` | 3 | `65124004` | Swelling (finding) | generic finding; document keeps plain text |
| Night sweat | `night sweat` | 3 | `42984000` | Night sweats (finding) | C77 annotation |
| Weight loss | `weight loss` | 3 | `89362005` | Weight loss (finding) | |
| Fever | `fever` | 3 | `386661006` | Fever (finding) | |
| Pallor | `pallor` | 3 | `274643008` | Body pale (finding) | generic (whole-body) pallor finding |
| Bleeding tendency | `bleeding tendency` | 4 | `78596001` | Bleeding diathesis (disorder) | C42 annotation; best clinical match |
| Susceptibility to infection | `susceptibility to infection` | 4 | `102463001` | Susceptibility to infections, function (observable entity) | C42 annotation; observable entity, not a finding |
| Jaundice | `jaundice` | 3 | `18165001` | Jaundice (disorder) | C22 annotation |
| Cough | `cough` | 3 | `49727002` | Cough (finding) | C34 annotation |
| Breathlessness | `breathlessness` | 3 | `267036007` | Dyspnea (finding) | |
| Constipation | `constipation` | 3 | `14760008` | Constipation (disorder) | C18 annotation |
| Diarrhoea | `diarrhoea` | 3 | `62315008` | Diarrhea (disorder) | |
| Melaena | `melaena` | 3 | `2901004` | Melena (finding) | C18 annotation |
| Nipple discharge | `nipple discharge` | 3 | `54302000` | Discharge from nipple (finding) | C50 annotation |
| Uterine bleeding | `uterine bleeding` | 3 | `44991000119100` | Abnormal uterine bleeding (disorder) | C53-C55 annotation; extension code (US edition concept); no plain "uterine bleeding" in International edition |
| Uterine discharge | `uterine discharge`, `vaginal discharge` | 4 | `271939006` | Vaginal discharge (finding) | C53-C55 annotation; coded as the clinically-synonymous vaginal discharge |
| Dysuria | `dysuria` | 3 | `49650001` | Dysuria (finding) | C67, C68 annotation |
| Haematuria | `haematuria` | 3 | `34436003` | Blood in urine (finding) | C64-C68 annotation; source doc spells `Heamaturia` — corrected |

## Duration children

Duration questions (`sym.pain.duration`, `sym.swelling.duration`, `sym.cough.duration`,
`sym.uterinebleeding.duration`) carry no SNOMED code — they are answer-option strings
`days / weeks / months / years` per source doc.

## For pathologist review

- `Uterine bleeding` uses the US extension concept `Abnormal uterine bleeding`
  (`44991000119100`). Confirm this matches the intended symptom, or propose a local
  International-edition concept.
- `Uterine discharge` is coded as `Vaginal discharge` (`271939006`) — nearest
  synonym in the International edition.
- `Pallor`, `Bleeding tendency`, `Susceptibility to infection` are region-tagged
  `C42` in the source. Semantic alignment with marrow pathology is assumed.