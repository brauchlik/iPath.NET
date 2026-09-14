# Questionnaire Review Studio (Offline Viewer)

A zero-dependency, pure static HTML5 application for reviewing drafted FHIR R4 Questionnaires side-by-side with working notes and original pathologist source texts using the official NLM LHC-Forms rendering widget.

---

## 🚀 How to Open

Just double-click **`index.html`** in Windows Explorer or open it in any modern browser (`file:///...`).
No local web server, Node.js, Python, or internet connection is required.

---

## 📁 File Structure

```
viewer/
├── index.html         # Main application shell (open directly)
├── styles.css         # UI layout & medical review theme
├── viewer.js          # Controller logic (tab switching, search, LHC-Forms mounting)
├── data.js            # Pre-bundled data bundle (questionnaires, notes, originals)
├── build-bundle.ps1   # PowerShell build script to regenerate data.js
└── lforms/            # Embedded offline NLM LHC-Forms v38.7.2 engine
    ├── styles.css
    ├── assets/lib/zone.min.js
    ├── runtime.js
    ├── polyfills.js
    ├── main.js
    ├── fhir/lformsFHIRAll.min.js
    └── marked.min.js
```

---

## 🔄 How to Rebuild After Content Changes

Whenever you:
- Edit or add a new FHIR Questionnaire `.json` (under `blocks/` or `staging/`)
- Update or create companion `.notes.md` or original `.md` files
- Update `registry.json` (add new items, change status, or set `deprecated: true`)

Run the build script from PowerShell:

```powershell
cd import\fhir\Questionnaires\viewer
.\build-bundle.ps1
```

### What `build-bundle.ps1` does:
1. Reads `../registry.json`
2. Inlines all referenced FHIR JSONs, Markdown notes, and source texts into `data.js` as `window.QUESTIONNAIRE_REGISTRY_DATA`
3. Allows the application to run via `file:///` without triggering browser CORS restrictions
