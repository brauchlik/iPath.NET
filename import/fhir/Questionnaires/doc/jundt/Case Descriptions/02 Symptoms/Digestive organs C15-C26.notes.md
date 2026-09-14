# Notes — Digestive organs C15-C26

Companion notes for `Digestive organs C15-C26.md` (pathologist original stays untouched).
Branch: `MAIN` · Server: `http://basyssrvdock1:8082` · Started: 2026-08-25 · Updated: 2026-08-26

## Structure

```
Dysphagia / Heartburn / Painful swallowing / Regurgitation / Dyspepsia   boolean
Pain (abdominal)                       boolean
├── location                           choice (single): upper/middle/lower abdomen × l/r, No data
└── duration                           quantity (d/wk/mo/a)
Flatulence                             boolean
Weight loss                            boolean + kg (quantity)
Vomiting                               boolean + "Vomiting blood" (boolean)
Constipation / Diarrhoea / Stool changes / Melaena / Bloody stool        boolean
Fever / Jaundice / Easy bruising       boolean
other                                  string ("please specify")
```

## Overlap Map & Deduplication Policy

The 6 symptoms below appear in both `Symptoms general.md` and `Digestive organs C15-C26.md`. They are strictly mapped to identical canonical `linkId`s and SNOMED CT codes in `blocks/coding-registry.json`:

| Symptom | Canonical `linkId` | SNOMED CT Code | FSN / Display | Deduplication Rule |
|---|---|---|---|---|
| Weight loss | `weightloss` | `89362005` | Weight loss | Digestive variant wins (includes `weightloss.kg` child) |
| Fever | `fever` | `386661006` | Fever | Identical; deduplicated at composition |
| Jaundice | `jaundice` | `18165001` | Jaundice | Identical; deduplicated at composition |
| Constipation | `constipation` | `14760008` | Constipation | Identical; deduplicated at composition |
| Diarrhoea | `diarrhoea` | `62315008` | Diarrhea | Identical; deduplicated at composition |
| Melaena | `melaena` | `2901004` | Melena | Identical; deduplicated at composition |

## Decisions & Questions for Jundt

- [x] Pain location modelled as a single combined choice (7 options) — answered 2026-08-25.
- [x] Overlap policy: same question in general + region → same SCT coding; dedupe at composition (region/richer variant wins) — answered 2026-08-25.
- [ ] Flatulence: `249504006` "Passing flatus" (chosen) vs `162076009` "Excessive upper gastrointestinal gas" — confirm.
- [ ] Abdominal-pain location options stay plain text (no clean region-laterality concept set) — confirm.
- [ ] "other" is a free-text string — confirm.

## SNOMED lookups

| Question | Code | Display | Notes |
|---|---|---|---|
| Dysphagia | `40739000` | Dysphagia | direct hit |
| Heartburn | `16331000` | Heartburn | direct hit |
| Painful swallowing | `30233002` | Swallowing painful | syn "Odynophagia" |
| Regurgitation | `78104003` | Regurgitation of gastric content | GI sense chosen over `302769004` |
| Dyspepsia | `162031009` | Indigestion | syn "Dyspepsia" |
| Flatulence | `249504006` | Passing flatus | alt `162076009` (see Q) |
| Vomiting | `422400008` | Vomiting (disorder) | verified |
| Vomiting blood | `8765009` | Hematemesis | |
| Stool changes | `88111009` | Altered bowel function | syn "Change in bowel habit" |
| Bloody stool | `405729008` | Hematochezia | distinct from Melaena `2901004` |
| Easy bruising | `424131007` | Easy bruising | |
| Pain (abdominal) | `21522001` | Abdominal pain | distinct canonical id `pain.abdominal` (general `pain` = any pain) |
| Weight loss | `89362005` | Weight loss | shared canonical; kg child uses UCUM `kg` |
| Constipation/Diarrhoea/Melaena/Fever/Jaundice | shared canonical ids | see `Symptoms general.notes.md` | |

## Refinements

- `pain.abdominal` boolean; children: `location` (choice, single) + `duration` (quantity, unitOption), each `enableWhen` parent = true.
- `weightloss` boolean + child `weightloss.kg` quantity (UCUM `kg`).
- `vomiting` boolean + child `vomiting.blood` boolean (Hematemesis).
- `other` = `string`.

## Blocks

- `symptoms.digestive` — topography `["C15-C26"]`.
