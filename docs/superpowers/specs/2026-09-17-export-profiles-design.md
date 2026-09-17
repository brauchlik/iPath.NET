# Export Profiles, Value Search and the Case Export — design capture

> **Status: proposed, decisions taken, not implemented.** Captured 2026-09-17 so the plan is not
> lost. Written before the review round with Dr. Jundt; if that review changes any decision in §8,
> change it here first — the decisions are the reason this document exists. No code exists for
> anything described here.
>
> Not to be confused with `2026-09-17-sdc-answer-extraction-design.md`, which describes what *is*
> built. This file is the parking place for the next step, not a delivered design.

**Roadmap position:** closes Sprint 3 (catalog ✅, column picker ✅, **CSV export open**) and carries
Sprint 4 (cohort filters — form ✅, **period** and **answer value** open).

## 1. Purpose

The review views can show the answer pivot and let a reviewer tick the columns. Three things are
missing before this is a research instrument rather than a viewer:

1. **The selection must survive the session.** Today it is page state: it is lost on navigation and
   cleared when the group changes, so no two people and no two months can compare the same columns.
2. **Cases must be selectable by what the answers say** — "search by values in Form" — not only by
   group, form and free text.
3. **The result must leave the screen as a file.** The feature is called *Data Export*; there is no
   download yet (verified: no CSV/Excel package is referenced anywhere in the repo).

## 2. The model: cohort + columns

The two items are independent halves, which is why they can be designed together:

```
ExportProfile = Cohort  +  Columns  +  Options
                 │          │          │
   which cases ──┘          │          └─ delimiter, include-empty-columns, filename scheme
   groupId | search         └─ which concepts, in which order
   (text, body site,           (form ids + concept keys from the catalog)
    period, value criteria)
```

**"Search by values in Form" is the cohort's value criteria** — the same criteria whether used ad hoc
on the page or stored inside a profile. A profile is therefore free-standing: its scope is either a
group *or* a search, both expressed in the same fields (decisions 2 and 3).

## 3. What already exists (do not rebuild)

| Need | Existing primitive |
|---|---|
| Cohort: group **or** search | `GetServiceRequestListQuery` / `GetServiceRequestsQueryBase` (`RequestFilter` ∈ None/Group/Owner/Search/NewCases/NewAnnotations, `GroupId`, `CommunityId`, `SearchString`), applied by `GetServiceRequestExtension.ApplyRequest` |
| "All matching case ids" | `GetServiceRequestIdListQuery.From(q)` — the same query with `PageSize = null`, `Page = 0` |
| Permissions | `ServiceRequestIsVisibleSpecifications` — already the filter inside the answers handler |
| Body-site picking UI | `TopoTreeFilter` + `NotificationBodySiteFilterDialog` (body site is a JSON complex property there) |
| Stable concept identity | `AnswerConcept.KeyFor` / `SystemRank` / `IsGeneratedKey`; `AnswerCatalogBuilder` already merges forms by key |
| Cell rendering | `AnswerCellFormatter.Format` / `JoinValues` — pivot and dialog already share it |
| File delivery | `Results.File(stream, contentType, fileDownloadName)`, as used in `DocumentEndpoints.cs:56` and `FhirEndpoints.cs:37` |
| Cohort search semantics | the answers handler already selects *cases that have a matching answer*, then shows those cases **complete** (`ServiceRequestAnswerQueryHandlers.cs:181-196`) — the right shape for a criterion |

## 4. Phase 1 — Export Profiles (persistence)

1. **Entity** `ExportProfile`
   - `Id`, `Name`, `Description`, `OwnerId`, `Visibility` (private / shared), `CreatedOn`, `UpdatedOn`, `LastUsedOn`
   - `GroupId?` — set when the cohort is a group
   - `Cohort` — **JSON complex property**: `RequestFilter`, `CommunityId?`, `SearchString?`, body site, period, `Criteria[]` (see §5). JSON keeps new filter kinds migration-free, the same trick as `QuestionnaireSettings`.
   - `QuestionnaireIds` — JSON, the selected forms; empty means all forms in the catalog
   - `ConceptKeys` — JSON, **ordered**, the selected columns (`AnswerConcept` keys)
   - `Options` — JSON: delimiter, include-empty-columns, filename scheme
   - One new table → the first migration in this area.
2. **Application**
   - `SaveExportProfileCommand` (create/update), `DeleteExportProfileCommand`, `GetExportProfilesQuery` (mine + shared with me), `GetExportProfileQuery`.
   - Validation lives in the handler: cohort must resolve and be permitted; concept keys normalised through `AnswerConcept`; keys that no longer exist in the catalog are **reported, not silently dropped**.
3. **Endpoints** `api/v1/admin/export/profiles*`, `RequireAuthorization("Developer")` for now (decision 5).
4. **UI** on `/admin/answers`: profile select beside the group picker plus *Save as new / Update / Delete*, and a "modified" marker when the live state differs from the loaded profile (same shape as `DiffersFromShipped` in `Translations.razor`). Loading a profile sets the cohort, the form filter and `_selectedColumns`, then reloads — `GetAnswerTableQuery` already tolerates `GroupId == null`, so a search scope works with no query change.
5. **Sharing** (decision 3): private by default, optional share with the group. Reuse the existing visibility vocabulary rather than inventing a permission system.

## 5. Phase 2 — Search by values in Form

6. **Criteria model**, one per concept taken from the catalog:
   `{ ConceptKey, Operator, Value?, Unit? }`
   - **AND across concepts, OR within a concept** (a repeating item matches any of its values).
   - Operators for this iteration: `eq`, `neq`, `contains`, `set`, `not set` — where *set* / *not set* answers "was this question ever answered?", which is itself a research cohort ("all cases where the Ki-67 was stated").
   - **No numeric or date ranges yet** (decision 1: *we don't deal with numeric yet*). That is why no typed columns are added now: `service_request_answers.Value` stays text and ranges wait until a query needs `> x`. The cohort's **period**, by contrast, filters the case (`ServiceRequest.CreatedOn`) and stays in Phase 1 — it needs no typed columns.
7. **UI**: on a ticked catalog row, a condition editor whose input depends on `ValueType` — boolean → tri-state, coding/choice → select from the catalog `Options`, string → text with `contains`, plus *set / not set*. The free-text search box stays as the quick path.
8. **Query**: extend the existing "case has a matching answer" pattern to a criteria list in one internal helper shared by the pivot and the export, so the two cannot drift.
9. **Indexes**: `service_request_answers` is indexed on `ServiceRequestId`, `Code`, `QuestionnaireId` — **not** on `Value`. `eq` on a coded answer is cheap; `contains` scans. Data volume is still tiny: measure before adding an index.

## 6. Phase 3 — CSV export

10. A dedicated export query returning all rows unpaged (`PageSize = null`, as `GetServiceRequestIdListQuery` does), sharing the selection helper of §5 item 8.
11. Endpoint returning `Results.File(bytes, "text/csv", $"{name}-{yyyy-MM-dd}.csv")`.
12. Format: UTF-8 **BOM**, **semicolon** separator so Excel in a German locale opens it in columns; header = concept label + code in profile order; one row per case.
13. Content (decision 4): **matrix + case identity** — title, date, accession no, body site, then one column per selected concept, rendered exactly as the pivot renders it (`AnswerCellFormatter`).
14. Both an ad-hoc export of the current selection and an export of a loaded profile; filename from the profile name plus the date.

## 7. Gotchas found while researching

- The answers table handler **overrides** the "null means all rows" convention: `pageSize = request.PageSize is > 0 ? request.PageSize.Value : 25`. `GetPageAsync` elsewhere treats `PageSize == null` as *no Skip/Take*. The export path must not rely on the table query's default — give it its own unpaged query, or fix the convention deliberately.
- `PagedQuery.Filter` (the `"Description.Title eq value"` node DSL in `ServiceRequestFilter.cs`) is reached only through the Refit clients; no UI component sets it. It is not a UI pattern to follow, and it knows nothing about answers.
- `service_request_answers` has no index on `Value`, by design (`…-sdc-answer-extraction-design.md` §2.1).
- `Value` is text only; there is **no** CSV/Excel package, and none of the JS interop files (they live in the RCLs — `iPath.RazorLib/wwwroot/js/*`, `iPath.LHCForms/wwwroot/*`) has a file-download helper. The download should go through `Results.File`, which the app already uses.
- Four providers exist (`Sqlite` live, Postgres and SqlServer snapshots stale): keep filter SQL provider-neutral (as the codebase does with `EF.Functions.Like`) — a reason to avoid casting text to numbers in SQL.
- Free-text search in the answers handler already covers `LinkId`, `Code`, `CodeDisplay`, `Value` and `ValueDisplay`; structured criteria are an addition, not a replacement.

## 8. Decisions (taken 2026-09-17)

| # | Question | Decision |
|---|---|---|
| 1 | Numeric/date ranges in value filters? | **No numeric yet** — text equality, contains and set/not-set; no typed value columns in this iteration |
| 2 | How is a profile scoped? | **Free-standing profile with the scope inside** (group or search) |
| 3 | Who sees a profile? | **Private, with an optional group share** |
| 4 | What is in the exported file? | **Matrix + case identity** (title, date, accession no, body site) |
| 5 | Who may use it? | **Developer now → later Admin → then group members** |
| 6 | Which format? | **CSV only** (UTF-8 BOM, semicolon) |

## 9. Next time — first steps

- [ ] Confirm the six decisions still hold after the review round (change this file first if not).
- [ ] `ExportProfile` entity + EF configuration + migration (`dotnet ef migrations add`, run by the developer for Sqlite, then Postgres/SqlServer).
- [ ] Commands/queries + handler validation (cohort resolvable and permitted, concept keys normalised and reconciled with the catalog).
- [ ] Endpoints under `admin/export/profiles`, Developer-gated.
- [ ] UI: profile select, save as new / update / delete, modified marker.
- [ ] Criteria model + condition editor (§5), reusing the catalog's `Options`.
- [ ] Unpaged export query + CSV writer (escaping, BOM, semicolon) + `Results.File` endpoint.
- [ ] Tests: catalog-key ↔ criteria-key consistency, criterion semantics (AND/OR, set/not-set, repeats), profile round-trip, CSV escaping and header order.
- [ ] CHANGELOG bullets under the current version; regenerate `openapi.json` (Debug build) once endpoints change.

## 10. Out of scope

Documents and WSI in the export · scheduled/background exports · xlsx workbooks · numeric and date
ranges **on answer values** and the typed columns they would need (the case period is in scope) ·
annotation responses · FHIR `Observation` projection · de-identification and compliance review ·
governance of who may export what (only touched by decision 5 for now).
