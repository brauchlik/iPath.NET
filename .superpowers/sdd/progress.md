# CaseRoom Implementation — Progress Ledger

Branch: `feature/caseroom-collaborative-slide-viewing`
Plan: `docs/superpowers/plans/2026-06-27-caseroom-collaborative-slide-viewing.md`

## Tasks

- Task 1: Domain models and sync contracts — pending
- Task 2: Extend `INotificationEventBus` with CaseRoom channel — pending
- Task 3: `ICaseRoomSessionStore` in-memory implementation — pending
- Task 4: CaseRoom API endpoints — pending
- Task 5: SSE integration — `SseClientService` + `ipath-sse.js` — pending
- Task 6: `IPathApi` Refit methods + `DirectApiClient` implementations — pending
- Task 7: WASM and Server implementations of `ICaseRoomSyncService` / `ICaseRoomSyncReceiver` — pending
- Task 8: JS interop module — `ipath-caseroom.js` — pending
- Task 9: `CaseRoomPage.razor` — Blazor page with inline OSD — pending
- Task 10: "CaseRoom active" badge on ServiceRequest page — pending
- Task 11: Final build + manual test plan — pending

## Completion Log
- Task 1: complete (commits c2be3a3..3400c48, review clean). Minor: missing trailing newlines in 3 new files (deferred to final review).
- Task 2: complete (commits 3400c48..3c6e6d1, review clean).
- Task 3: complete (commits 3c6e6d1..33089af, review clean). Minors: unnecessary ContinueWith overload expansion, weak Sync_UpdatesViewport assertion (plan-mandated), microscopic TOCTOU window in teardown (plan-mandated design), GetOrAdd factory allocation waste — all deferred to final review.
- Task 4: complete (commits 33089af..d1411a7, review clean). Minors: no explicit [FromBody] (brief-mandated), GET status has no IUserSession audit (brief-mandated). **IMPORTANT NOTE for Tasks 6/7/9: SessionUserDto has Username (not DisplayName) — use sess.User.Username (non-nullable).**
- Task 5: complete (commits d1411a7..555cd26, review clean). Minor: only happy-path test (brief-mandated 1-test count). Out-of-brief: csproj added ProjectReference to iPath.RazorLib (reviewer verified necessary — iPath.Blazor.ServiceLib does not transitively reference iPath.RazorLib).
