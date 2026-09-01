# Notes — General diagnostic assessment fields

Companion notes for `General diagnostic assement fields.md` (pathologist original stays untouched).
Branch: `MAIN` · Server: `http://basyssrvdock1:8082` · Started: 2026-08-26

## Structure

```
Margin status (all Tumors)                       choice (single)
├── R0 microscopically negative surgical margins
├── R1 microscopically positive surgical margins
├── R2 macroscopically incomplete with gross residual tumor
└── No data
Soft tissue Tumours (& Bone)                     group
├── Size                                         choice (single)
│   ├── Size < 5cm
│   ├── Size > 10cm
│   └── Size: no data
├── Depth                                        choice (single)
│   ├── Superficial (above fascia)
│   ├── Deep (below fascia/intramuscular)
│   └── Depth: no data
└── Previous radiation therapy                   choice (single)
    ├── Yes
    ├── No
    └── No data
Breast [C50-C50]                                group (gated / C50 scope)
├── Family history of breast or ovarian cancer  choice (single: Yes/No/Unknown)
├── Other cancers in personal clinical history  choice (single: Yes/No)
└── Microcalcifications on imaging              choice (single: Yes/No/No data)
```

## Questions for Jundt

- [ ] Margin status: Coded as UICC R0/R1/R2 choice options. Confirm whether R0/R1/R2 codes are preferred over SNOMED finding concepts (443930003 clear / 443929007 involved).
- [ ] Soft tissue tumor fields: Source notes "(Gilt auch für Knochen!)" (Also applies to bone). Confirm whether Size and Depth apply to both Soft Tissue and Bone tumors.
- [ ] Breast C50: Confirm whether Breast diagnostic fields should be isolated in a `diagnostic.breast` block or kept inside general diagnostic assessment.

## SNOMED lookups

| Question / Option | Code | Display | Notes |
|---|---|---|---|
| Surgical margin status | `396631000` | Surgical margin status | Observable entity |
| R0 microscopically negative | `443930003` | Surgical margin clear | UICC R0 |
| R1 microscopically positive | `443929007` | Surgical margin involved | UICC R1 |
| R2 macroscopically incomplete | `396632007` | Gross residual tumor present | UICC R2 |
| Tumor size | `371024008` | Tumor size | Observable entity |
| Superficial depth | `371003006` | Superficial tumor | Finding |
| Deep depth | `371004000` | Deep tumor | Finding |
| Previous radiation therapy | `416237000` | History of radiation therapy | Finding |
| Family history of breast cancer | `416474007` | Family history of malignant neoplasm of breast | Finding |
| Family history of ovarian cancer | `44054006` | Family history of malignant neoplasm of ovary | Finding |
| History of other cancers | `161491000` | History of malignant neoplasm | Finding |
| Microcalcifications | `129749004` | Microcalcification of breast | Finding |

## Refinements

- All fields use explicit "No data" / "Unknown" options to distinguish unanswered vs unknown.
- Margin status: `choice` item with 4 options.
- Breast fields: Gated behind ICD-O C50 scope or Breast block.

## Blocks

- `diagnostic.general` — topography `[]` (Margin status, Soft tissue & Bone size/depth/radiation).
- `diagnostic.breast` — topography `["C50"]`.
