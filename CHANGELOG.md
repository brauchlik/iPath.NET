# Changelog

## 0.3.1

- Removed the CodeBeam.MudBlazor.Extensions dependency: the JSON panels are plain read-only text fields again and the preview-mode selects use MudBlazor's own MudSelect. Two of its components had caused failures - MudSelectExtended needs a service that was registered server-side only, and MudCodeViewer's JS helper dereferences an unresolved element reference without a null check
- Fix: the questionnaire admin page failed to render under WebAssembly - the text preview services and their registry were registered server-side only; they now come from one shared `AddQuestionnaireToTextServices()` used by both the API and the RazorLib registration
- Questionnaire text preview: the mode used when a questionnaire has none selected is now configurable (`iPathClientConfig:DefaultTextPreviewService`) instead of hardcoded to "Default List"
- Symptoms blocks: `menopause.irregularities` moved up to be a sibling of `menopause.regularcycle` (it was a grandchild, so the text preview dropped it)
- Fix: the questionnaire admin page could throw a NullReferenceException while the questionnaire was still loading - `EditQuestionnaireModel.Settings` defaulted to null and the tabs read it during that render
- Docs: AGENTS.md records that `openapi.json` is build-generated and committed, and `.gitignore` now covers CodeRush's `.cr/` folder and the `data/` scratch directory

## 0.3

- Annotation replies (threaded comments), with `AddAnnotationReplyTo` migration
- Selectable text-preview modes per questionnaire: Default List, CSV, Case Description (Compact / Expanded)
- Text preview now emits HTML: bold group headers, section spacing, arrow detail lines
- Text preview fixes: no blank line after each detail, no duplicated labels, groups with only details no longer dropped
- Questionnaire admin: FHIR upload now persists through the API handler and rebinds the JSON viewer (was parsed locally and discarded)
- Questionnaire admin: upload shows "processing ..." instead of MudBlazor's selected-file chip
- New-questionnaire dialog: restored the FHIR upload button (the `ActivatorContent` block was silently ignored in MudBlazor 9) and dropped the file chip
- Questionnaire admin: file selection is cleared after upload
- Questionnaire admin: FHIR source moved behind a collapsed, read-only panel showing id/version/size (copy button, no height cap)
- Text preview: group header is always shown, so a group with only detail lines stays labelled
- Material block: cytology/histology material types are now child items of their gate boolean (block plus both staged case descriptions)
- Lab block consolidated: `lab.blood-count` renamed to `lab.hematology`; the duplicate generic `lab.json` removed (its nested shape dropped the differential in the preview)
- LHC-Forms 38.7.2 -> 44.0.0; bundle re-sourced from the npm package
- Un-layered the vendored LHC-Forms CSS - MudBlazor's unlayered preflight was zeroing every form border
- Guarded LForms `_codingsEqual` against null - it was aborting skip logic and leaving sub-items collapsed
- Added CodeBeam.MudBlazor.Extensions: `MudCodeViewer` for JSON, `MudSelectExtended` for dense selects
- Dependency updates: MudBlazor 9.10, Hl7.Fhir.R4 6.5, Dapper 2.1.86, MailKit 4.18, Aspire 13.5.4
- Version centralized in `Directory.Build.props`
- ServiceRequest delete fixed (soft-delete instead of EF Remove)
- WSI slideshow: tiles now paint, OSD pan works, OSD errors surface instead of a black box
- Prune expired DataProtection keys on startup
- Docs: `OPERATIONS.md` plus DataProtection section in `INSTALLATION.md`
- Null-guard for `PatientInfo` in the description view
- CaseRoom button icon `Group` -> `Share`
