# SDC Answer Extraction — Design and Implementation (Sprints 1–3)

Status: Sprints 1–2 implemented, Sprint 3 partly (catalog and column picker; CSV export open), version 0.3.3
Scope: extract questionnaire answers at input time, and give developers and admins a direct, export-oriented view of the result.

## 1. Purpose

`QuestionnaireResponse` data is user-defined and form-local. Its `linkId`s are presentation
shortcuts (`sym.dysphagia` in one form is `sym.other.dysphagia` in another), while the clinical
identity of a question is its coding. So answers have to be **resolved to concepts once, while the
definition is still available**, and everything presentational has to happen later.

The rule the work follows:

> **Resolve at input, present at export.**

This is the FHIR *Structured Data Capture* idea (a QR extracted into discrete, coded, queryable
data). We deliberately deviate in one respect: **no FHIR resources are created**. Extraction writes
answer rows shaped like the `Observation` core (code, value, unit, provenance) with no `status` and
no `subject`, so no lifecycle semantics are inherited. A future `Observation` projection stays
possible and mechanical.

## 2. Data model

### 2.1 `service_request_answers` (new table)

Derived data. Rows are deleted and rebuilt per case; the stored `QuestionnaireResponse` remains the
source of truth.

| column | purpose |
|---|---|
| `Id` | |
| `service_request_id` | the case (FK, cascade delete) |
| `QuestionnaireId`, `QuestionnaireVersion` | which definition was answered |
| `LinkId` | form-local path, for audit and for dummy-key derivation |
| `CodeSystem`, `Code`, `CodeDisplay` | **question identity** — the preferred coding |
| `OtherCodings` | the remaining codings, as `system\|code\|display;…` |
| `ValueType` | `boolean` / `quantity` / `integer` / `decimal` / `string` / `date` / `coding` / `attachment` |
| `Value` | the answer as text; for coding answers `system\|code` |
| `ValueDisplay` | human-readable value |
| `Unit` | e.g. `kg`, `%`, `10*9/L` |
| `ExtractionVersion`, `CreatedOn` | which extractor produced the row |

Indexed on `service_request_id`, `Code`, `QuestionnaireId`. `Value` is deliberately not indexed:
values are text and cohort filters start with equality/`LIKE` semantics.

`Code*` is **what was asked**, `Value*` is **what was answered**. That separation is what makes a
`choice` item carrying a coded answer work at all.

### 2.2 `QuestionnaireResponseData` (JSON property — no migration)

`ExtractedOn`, `ExtractionVersion`, `ExtractedAnswerCount`, `ExtractionError`. Also `Version`, which
is now actually written: it was always null before, so responses were rendered against whatever
definition happened to be active.

### 2.3 `QuestionnaireSettings` (JSON property — no migration)

`ExtractAnswers` (bool, default **true**). The extractor skips forms that are switched off, and the
rows of those cases are removed, so an excluded form contributes nothing.

## 3. Extraction rules

1. **Only answered items produce rows.** No row means *not observed*. `enableWhen` is never
   evaluated: gate-false and skipped-by-the-physician are deliberately indistinguishable.
2. `false` is an answer and produces a row.
3. Repeating items produce one row per selected value.
4. Concept key = the item's coding, preferred **SNOMED CT > LOINC > anything else > dummy**;
   the remaining codings go to `OtherCodings`.
5. Dummy key = the nearest coded ancestor's coding plus the remaining path
   (`sym.pain.abdominal.location` and `sym.other.pain.abdominal.location` both resolve to
   `snomed|21522001.location`). With no coded ancestor the whole `LinkId` becomes the key under
   `http://ipath.net/fhir/CodeSystem/questionnaire-item`.
6. Answer mapping: boolean, integer, decimal, quantity (+ unit, falling back to the item's
   `questionnaire-unitOption`), string, date, coding (`system|code`, display), uri/url, attachment.
7. Nothing is curated away. Gates such as `sym.other.present` are extracted like anything else and
   are simply not selected in an export.
8. **Failure is non-fatal and never silent**: the error is logged and recorded on the response, no
   new rows are written, and rows from the last successful run are kept. Saving the case always
   succeeds. This is deliberately *not* the behaviour of `GeneratedText`, which blanks on error.
9. Re-extraction is idempotent: the case's rows are replaced.
10. Legacy responses (no `Version`) have the active version resolved and **pinned** on first
    extraction, so the backfill heals missing versions as it goes.

## 4. Components

| piece | location |
|---|---|
| `QuestionnaireItemIndex` | `iPath.Application/Features/Questionnaires/` — shared index, coding preference and key resolution used by extractor *and* checker |
| `IQuestionnaireAnswerExtractor` / `QuestionnaireAnswerExtractor` | same folder; pure `(response, questionnaire) → rows` |
| `IQuestionnaireConformityChecker` / `QuestionnaireConformityChecker` | same folder; diagnostics over the same rules |
| `ServiceRequestAnswerExtractionService` | `iPath.Database.EFCore/FeatureHandlers/Questionnaires/Services/` — single implementation used by both the save path and the backfill; does not call `SaveChanges` |
| write at save | `UpdateServiceRequestHandler` — beside `GeneratedText`, where the definition is already loaded; one unit of work for rows and description |
| re-extract / backfill | `ReExtractServiceRequestAnswersCommand`, `BackfillServiceRequestAnswersCommand` |
| queries | Sprint 2: `GetAnswersByCaseQuery`, `GetAnswersByFilterQuery`, `GetAnswerExtractionIssuesQuery`, `GetQuestionnaireConformityQuery`. Sprint 3: `GetAnswerCatalogQuery` (the group catalog) and `GetAnswerTableQuery` (the pivot, whose `Columns` carries an explicit, ordered selection) |
| catalog and selection | `AnswerCatalogBuilder` (merges the group's forms by concept key, pure), `AnswerConcept` (the single key/rank authority, shared by extractor, builder and handlers), `AnswerCellFormatter` (one cell per case and concept, shared by pivot and dialog) |
| endpoints | `api/v1/admin/answers/*` and `api/v1/admin/questionnaires/{id}/conformity`, all `RequireAuthorization("Developer")` |
| admin UI | per-case dialog (opened from the admin toolbar beside the mail preview), `/admin/answers` — group first: forms, catalog (which doubles as the column picker), answers pivot, extraction issues and backfill; "Check extraction" on the questionnaire admin page; reachable from the Developers menu as *Data Export* |

## 5. Delivered in this iteration

Sprint 1: version pinning (which also fixes `GeneratedText` being rendered against the active
definition), extractor, table, save-time extraction, extraction state, the `ExtractAnswers` flag,
failure policy, re-extract/backfill, unit tests.

Sprint 2: the two direct views of the rows — a per-case dialog (Developer-gated, like the answers
API) showing the extraction state and rows with a re-extract action, and a paged group-level list at
`/admin/answers` with a questionnaire filter, free-text search and an **extraction issues** section
(cases whose response was never extracted or whose attempt failed, with a backfill trigger). Plus the
conformity checker output on the questionnaire admin page.

Sprint 3 (partly): the catalog is written from real data rather than from theory —
`AnswerCatalogBuilder` merges the group's case-description forms by concept key, so which concepts
actually occur, which are noise and which collide is read off the answers themselves. On top of that
`/admin/answers` was rebuilt group first (forms with a per-form conformity summary → catalog →
answers pivot, the long per-answer list is gone) and the catalog doubles as a column picker: a ticked
selection is rendered as sent, in order, **including columns no case answered**, which is what makes
two exports comparable, with a caption reporting how many concepts in the data are outside the
selection. Still open from this sprint: the CSV export.

Two signals the review depends on: *response present but zero rows*, and *dummy-key rows* (items
that still need a real coding).

## 6. Verification

- `dotnet test`: 15 new tests, full suite green. Cases: answered true/false, unanswered produces no
  row, quantity child inheriting the parent's code and unit, the cross-form collapse
  (`21522001.location`), SNOMED preferred with LOINC retained, repeats per value, uncoded gate under
  the dummy system, coded answer keeping its own code, null/empty input, an answer without a
  definition, plus four conformity-checker cases.
- Solution builds clean; `openapi.json` regenerated and committed because new endpoints were added.
- Sprint 3: catalog and column selection verified against the live Sqlite database (one group: 3
  cases, 2 forms, 67 concepts of which 11 have no code; an explicit 4-column selection came back in
  the order sent, with never-answered and unknown columns empty, and `conceptsOutsideColumns`
  consistent with the observed columns). Full suite green (153 passing, 9 skipped).
- Not yet verified at runtime: whether LForms emits an explicit `false` for an answered "no" and
  omits untouched items. Rule 1 depends on it. It is observable from the review views
  (`ExtractedAnswerCount` per case), so the first filled form will confirm or refute it.

## 7. Roadmap and what remains

| sprint | scope | state |
|---|---|---|
| Sprint 1 | extractor, `service_request_answers`, version pinning, re-extract/backfill | done |
| Sprint 2 | the two review views (per-case dialog, group list), conformity checker | done |
| Sprint 3 | catalog, column picker, CSV export | catalog and picker done, **CSV export open** |
| Sprint 4 | cohort filters: period, form, answer value | form done, **period and answer value open** |
| then | governance; questionnaire version-deletion policy | not started |

The three open items are one coherent piece of work: a saved export profile is the cohort plus the
column selection, and the CSV export is what applies it. Captured in
`2026-09-17-export-profiles-design.md`, with the decisions that piece needs.

Still deliberately out of scope: annotation responses · FHIR `Observation` projection · numeric
typing of values (typed columns come when a filter needs `> x` — the export capture decides against
them for now) · permissions beyond reusing case visibility · de-identification and compliance review.

Separate sprint: questionnaire lifecycle — refuse deletion of a version referenced by any response,
and close the "new version on every save" TODO. Extracting at input is what makes deferring this
safe for existing rows.
