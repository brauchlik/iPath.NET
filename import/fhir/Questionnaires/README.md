# iPath.NET — FHIR Questionnaire Authoring & Review Workspace

This workspace manages the conversion of clinical source texts (e.g. from Dr. Jundt) into standardized FHIR R4 Questionnaires for iPath.NET.

---

## 📂 Directory Overview

```
import/fhir/Questionnaires/
├── registry.json       # Central catalog of all Building Blocks and Case Descriptions
├── blocks/             # Reusable minimal FHIR R4 Questionnaires
│   ├── coding-registry.json  # Canonical terminology & SNOMED CT mappings
│   ├── 01 Material/    # Specimen & biopsy blocks
│   ├── 02 Symptoms/    # Symptoms blocks (generic & organ-specific)
│   ├── 03 Imaging/     # Imaging / radiological findings blocks
│   ├── 04 Lab/         # Laboratory findings & blood counts
│   └── 05 Diagnostic/  # Histology & diagnostic blocks
├── doc/
│   └── jundt/          # Source drafts, companion notes, and meeting minutes
│       ├── Case Descriptions/  # Original .md and paired <name>.notes.md
│       ├── FEEDBACK.md         # Clinical decision log & questions
│       └── meeting-*.md        # Pathologist review session notes
├── staging/            # Assembled multi-block Case Description Questionnaires
└── viewer/             # Standalone Offline Review Studio (LHC-Forms widget)
```

---

## 🔬 Reviewing Questionnaires

To interactively review all drafted building blocks and case forms side-by-side with clinical notes and raw FHIR JSON:

1. Open **`viewer/index.html`** directly in any web browser.
2. Filter by category, topography, or search terms.
3. Test-fill fields in the live NLM LHC-Forms widget and click **"📥 View Response JSON"** to inspect generated `QuestionnaireResponse` resources.

---

## 🔄 Rebuilding the Viewer

Whenever questionnaires, companion notes, or `registry.json` are modified:

```powershell
# From the repository root or viewer folder:
.\import\fhir\Questionnaires\viewer\build-bundle.ps1
```

This compiles all files into `viewer/data.js` so the viewer can run 100% offline via `file:///`.
