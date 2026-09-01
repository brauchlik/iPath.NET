# Notes — Breast and female genital organs C50-C58

Companion notes for `Breast and female genital organs C50-C58.md` (pathologist original stays untouched).
Branch: `MAIN` · Server: `http://basyssrvdock1:8082` · Started: 2026-08-31

## Structure

```
Breast [C50]
├── Lump (breast)                                  boolean
│   ├── Duration present                           quantity (d, wk, mo)
│   ├── Increasing in size duration                quantity (d, wk, mo)
│   ├── Consistency                                choice (hard, smooth, unknown)
│   └── Border                                     choice (irregular, regular, unknown)
├── Nipple discharge                               boolean
├── Nipple retracted                               boolean
└── Breast-skin discolored                         boolean

Female Genital Organs [C51-C58]
├── Pregnancy                                      boolean
│   └── Gestational week                           integer
├── Previous pregnancies                           boolean
│   └── Date of last delivery                      date (or string: dd/mm/yyyy)
├── Menopause                                      boolean
│   ├── (if Menopause = Yes) Postmenopausal bleed boolean
│   └── (if Menopause = No) Regular cycle         boolean
│       └── (if Regular = No) Irregular periods    choice / multi-select:
│           ├── Heavy menstrual bleeding / Menorrhagia
│           ├── Abnormal bleeding / Spotting
│           ├── Missed periods / Amenorrhea
│           └── Painful periods / Dysmenorrhea
├── Lower abdominal pain                           boolean
│   ├── Duration                                   quantity (d, wk, mo)
│   └── Side                                       choice (right, middle, left)
├── Lower abdominal persistent swelling            boolean
│   ├── Duration                                   quantity (d, wk, mo)
│   └── Side                                       choice (right, middle, left)
├── Vaginal bleeding                               boolean
│   └── Duration                                   quantity (d, wk, mo)
└── Vaginal discharge                              boolean
    └── Duration                                   quantity (d, wk, mo)
```

## Decisions & Questions for Jundt

- [ ] Menstrual irregularities: Modelled as multi-select choice options under Premenopausal + Irregular cycle. Confirm.
- [ ] Delivery date: Coded as FHIR `type: date` (renders standard date picker `dd/mm/yyyy`).
- [ ] Pregnancy week: Coded as integer (`1-42` weeks).
- [ ] Breast lump characteristics: Consistency (`hard/smooth/unknown`) and Border (`irregular/regular/unknown`) carry explicit `"unknown"` options as requested by Dr. Jundt.

## SNOMED lookups

| Question / Concept | Code | Display | Notes |
|---|---|---|---|
| Breast lump | `274647000` | Breast lump | Finding |
| Hard consistency | `255395000` | Hard | Qualifier |
| Smooth consistency | `255397008` | Smooth | Qualifier |
| Irregular border | `255535002` | Irregular | Qualifier |
| Regular border | `255534003` | Regular | Qualifier |
| Nipple discharge | `54302000` | Discharge from nipple | Finding |
| Nipple retraction | `69085004` | Nipple retraction | Finding |
| Breast skin discoloration | `402773007` | Discoloration of skin of breast | Finding |
| Pregnancy | `77386006` | Pregnant | Finding |
| Gestational age | `1156895004` | Gestational age | Observable entity |
| Previous pregnancy | `161732006` | Gravida | Finding |
| Date of last delivery | `161714006` | Date of last delivery | Observable entity |
| Menopause | `426979002` | Menopause | Finding |
| Postmenopausal bleeding | `289530006` | Postmenopausal bleeding | Finding |
| Regular menstrual cycle | `289947009` | Regular menstrual cycle | Finding |
| Irregular periods | `80182007` | Irregular periods | Finding |
| Menorrhagia | `386692008` | Heavy menstrual bleeding | Finding |
| Intermenstrual bleeding / Spotting | `289535001` | Intermenstrual bleeding | Finding |
| Amenorrhea | `89217008` | Amenorrhea | Finding |
| Dysmenorrhea | `266599000` | Dysmenorrhea | Finding |
| Lower abdominal pain | `3006004` | Lower abdominal pain | Finding |
| Lower abdominal swelling | `285376008` | Lower abdominal swelling | Finding |
| Vaginal bleeding | `249021008` | Vaginal bleeding | Finding |
| Vaginal discharge | `271939006` | Vaginal discharge | Finding |

## Refinements

- Lump consistency and border choices include explicit `"unknown"` options.
- Conditional branching for Premenopausal vs Postmenopausal menstrual histories.
- Durations carry standard UCUM unit options (`d`, `wk`, `mo`).

## Blocks

- `symptoms` — Master Symptoms Block (topography tags `["C50"]` for Breast, `["C51-C58"]` for Female Genital Organs).
