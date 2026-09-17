# SDC Answer Extraction — Design and Implementation (Sprints 1–2)

Status: implemented on `main`, version 0.3.3
Scope: extract questionnaire answers at input time, and give admins a direct view of the result.

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
| queries | `GetAnswersByCaseQuery`, `GetAnswersByFilterQuery`, `GetAnswerExtractionIssuesQuery`, `GetQuestionnaireConformityQuery` |
| endpoints | `api/v1/admin/answers/*` and `api/v1/admin/questionnaires/{id}/conformity`, all `RequireAuthorization("Admin")` |
| admin UI | per-case dialog (opened from the admin toolbar beside the mail preview), `/admin/answers` review page, "Check extraction" on the questionnaire admin page |

## 5. Delivered in this iteration

Sprint 1: version pinning (which also fixes `GeneratedText` being rendered against the active
definition), extractor, table, save-time extraction, extraction state, the `ExtractAnswers` flag,
failure policy, re-extract/backfill, unit tests.

Sprint 2: the two direct views of the rows — an admin-only per-case dialog showing the extraction
state and rows with a re-extract action, and a paged group-level list at `/admin/answers` with a
questionnaire filter, free-text search and an **extraction issues** section (cases whose response
was never extracted or whose attempt failed, with a backfill trigger). Plus the conformity checker
output on the questionnaire admin page.

Two signals the review depends on: *response present but zero rows*, and *dummy-key rows* (items
that still need a real coding).

## 6. Verification

- `dotnet test`: 15 new tests, full suite green. Cases: answered true/false, unanswered produces no
  row, quantity child inheriting the parent's code and unit, the cross-form collapse
  (`21522001.location`), SNOMED preferred with LOINC retained, repeats per value, uncoded gate under
  the dummy system, coded answer keeping its own code, null/empty input, an answer without a
  definition, plus four conformity-checker cases.
- Solution builds clean; `openapi.json` regenerated and committed because new endpoints were added.
- Not yet verified at runtime: whether LForms emits an explicit `false` for an answered "no" and
  omits untouched items. Rule 1 depends on it. It is observable from the review views
  (`ExtractedAnswerCount` per case), so the first filled form will confirm or refute it.

## 7. Deliberately out of scope

Catalog of concepts, column picker, CSV export (Sprint 3) · cohort filters by period, form or answer
value (Sprint 4) · annotation responses · FHIR `Observation` projection · numeric typing of values
(typed columns come when a query needs `> x`) · permissions beyond reusing case visibility ·
de-identification and compliance review.

Separate sprint: questionnaire lifecycle — refuse deletion of a version referenced by any response,
and close the "new version on every save" TODO. Extracting at input is what makes deferring this
safe for existing rows.
