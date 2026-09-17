# Changelog

## 0.3.3

- Answer extraction (SDC): questionnaire answers are extracted into a new `service_request_answers` table when a case is saved - one row per answered item, keyed by the item's coding (SNOMED CT, then LOINC, then a generated key from the nearest coded ancestor), so one clinical question resolves to the same concept across forms
- Only answered items are extracted - no row means not observed; `false` is an answer. Extraction failures are logged and recorded per case and never break the save
- Questionnaires can now be excluded from extraction via `QuestionnaireSettings:ExtractAnswers` - a "Extract answers" checkbox on the questionnaire Settings tab; not configured counts as enabled, so an existing form can never be switched off by accident
- Fix: extraction was silently switched off for every questionnaire stored before that setting existed - a missing member in the settings JSON materialises as `false` rather than as the property's default, so the flag is nullable now and only an explicit `false` disables extraction
- Fix: a display-only coding (LForms writes answer options such as "Core biopsy" that way) now keeps its display text as the value instead of storing an empty `system|code`
- Fix: a failure in the answer extraction can no longer fail a case save - the no-response path is inside the same error handling as the rest
- Extraction state records when a form is switched off, so it is distinguishable from "extracted, nothing answered"; every extraction logs the row and item counts
- Responses record the definition version they were answered against, which was always null before - that also fixes the text preview being rendered against the currently active questionnaire version instead of the answered one
- Admin: a toolbar button on a case shows the extracted answers and the extraction state (rows, version, last error) and can re-extract; a new `/admin/answers` page lists answers per group with a questionnaire filter and free-text search, reports cases whose extraction is missing or failed, and can run the backfill
- Admin: "Check extraction" on the questionnaire page reports conformity problems using the extractor's own rules (duplicate linkIds, uncoded items and the generated key they would get, codings shared by two questions, non-extracted item types)
- Docs: `docs/superpowers/specs/2026-09-17-sdc-answer-extraction-design.md` records the extraction rules, the data model and what is deliberately left to later sprints

## 0.3.2

- Removed the built-in AI auto-translate pipeline (`TranslateKeysBatchCommand`/`Handler`, `ITranslationJobQueue`/`TranslationJobWorker`, the `admin/ai/translations/translate` endpoint, the "Auto-Translate" button on Admin > AI Status > Translations Manager, and the now-dead `LocalizationSettings:Active`/`AddMissingStrings` flags): it only ever saw a bare source string with no surrounding context, so it couldn't disambiguate short/generic phrases the way translating with a coding agent (see the `translation-update` skill) does. The manual per-key editor, the status/missing-key display, and the file-based translation store are untouched.
- Removed `src/ui/iPath.RazorLib/Localization/LocalizationService.cs`, an unused, unreferenced second `IStringLocalizer` implementation superseded by `StringLocalizerService`
- Removed `LocalizationSettings:AutoSave`: with the AI pipeline gone, its only remaining effect was pre-creating an empty locale file the first time a not-yet-existing locale was touched - both real write paths (the sync CLI's `--write` and `AutoUpdate`) always save real content immediately anyway, so the early empty write was dead weight

## 0.3.1

- Removed the CodeBeam.MudBlazor.Extensions dependency: the JSON panels are plain read-only text fields again and the preview-mode selects use MudBlazor's own MudSelect. Two of its components had caused failures - MudSelectExtended needs a service that was registered server-side only, and MudCodeViewer's JS helper dereferences an unresolved element reference without a null check
- Fix: the questionnaire admin page failed to render under WebAssembly - the text preview services and their registry were registered server-side only; they now come from one shared `AddQuestionnaireToTextServices()` used by both the API and the RazorLib registration
- Questionnaire text preview: the mode used when a questionnaire has none selected is now configurable (`iPathClientConfig:DefaultTextPreviewService`) instead of hardcoded to "Default List"
- Symptoms blocks: `menopause.irregularities` moved up to be a sibling of `menopause.regularcycle` (it was a grandchild, so the text preview dropped it)
- Fix: the questionnaire admin page could throw a NullReferenceException while the questionnaire was still loading - `EditQuestionnaireModel.Settings` defaulted to null and the tabs read it during that render
- Docs: AGENTS.md records that `openapi.json` is build-generated and committed, and `.gitignore` now covers CodeRush's `.cr/` folder and the `data/` scratch directory
- Fix: `GET /api/v1/translations/{lang}` returned HTTP 500 whenever `LocalizationSettings` was missing from a server's appsettings (because `SupportedCultures` then binds empty) - an unknown culture, an unconfigured `LocalesRoot` and an unreadable locale file now log a warning and return empty data instead of throwing, and an empty `SupportedCultures` falls back to `["en"]` with a warning
- Locale files are no longer seeded with the `Test`/`Test2` placeholder words when a missing file is auto-saved
- Locale files (`src/ui/iPath.Blazor.Server/Locales/{en,de,fr,it}.json`) are now tracked in the repo instead of living in the gitignored local data folder, and DE/FR/IT translations are complete for all 373 strings currently wrapped in `@T[...]`/`T[...]`
- Added `tools/iPath.LocalizationSync`, a CLI that statically scans the UI source for `@T[...]`/`T[...]` call sites and diffs them against the locale files (`sync`, `--write`, `--purge`), so drift can be found without running the app or clicking through every page in every language
- `LocalizationSettings:AutoUpdate` (default off) pushes new/updated keys from the shipped baseline locale files into a deployment's own configured `LocalesRoot` on startup, filling gaps without ever overwriting a translation already made there
- Fix: the admin mailbox compose dialog's ambiguous `T["Body"]` label is now `T["Message Body"]`
- Docs: added a `translation-update` skill (`.claude/skills/translation-update/`) documenting how to keep translations in sync before a release

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
