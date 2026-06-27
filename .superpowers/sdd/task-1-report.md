# Task 1 Report — Domain models and sync contracts

## Implementation summary

Three new files created (verbatim from the brief):

- `src/core/iPath.Application/Features/CaseRoom/CaseRoomModels.cs`
  — `ViewportState`, `Participant`, `SyncPayload`, `CaseRoomSnapshot`, `CaseRoomStatus`, `CaseRoomSyncEvent` records.
- `src/core/iPath.Application/Features/CaseRoom/ICaseRoomSyncService.cs`
  — `ICaseRoomSyncService : IAsyncDisposable` (JoinAsync/LeaveAsync/SyncAsync) and `ICaseRoomSyncReceiver` (IDisposable Subscribe).
- `test/iPath.Test.xUnit2/CaseRoom/CaseRoomModelsTests.cs`
  — 4 tests covering ViewportState construction, both SyncPayload nullability modes, and CaseRoomSyncEvent property access.

All types live in the new `iPath.Application.Features.CaseRoom` namespace. No existing files modified. No comments added. File-scoped namespaces used.

## TDD evidence

### RED — test written before implementation

Command:
```
dotnet test test/iPath.Test.xUnit2/iPath.Test.xUnit2.csproj --filter "FullyQualifiedName~CaseRoomModelsTests"
```

Relevant output (build failure — namespace does not exist):
```
test/iPath.Test.xUnit2/CaseRoom/CaseRoomModelsTests.cs(1,34): error CS0234:
  Der Typ- oder Namespacename "CaseRoom" ist im Namespace "iPath.Application.Features"
  nicht vorhanden. (Möglicherweise fehlt ein Assemblyverweis.)
```
Tests did not run — build failed as expected (Step 2 of the brief).

### GREEN — after creating both source files

Command:
```
dotnet test test/iPath.Test.xUnit2/iPath.Test.xUnit2.csproj --filter "FullyQualifiedName~CaseRoomModelsTests" --nologo --verbosity minimal
```

Relevant output:
```
iPath.Test.xUnit2 -> ...bin\Debug\net10.0\iPath.Test.xUnit2.dll
Testlauf für "...\iPath.Test.xUnit2.dll" (.NETCoreApp,Version=v10.0)
Bestanden!   : Fehler:     0, erfolgreich:     4, übersprungen:     0, gesamt:     4, Dauer: 24 ms - iPath.Test.xUnit2.dll (net10.0)
```

Result: **4/4 passing, output pristine** (no warnings attributable to the new files; remaining warnings are pre-existing in other projects).

## Commit

```
3400c48 feat(caseroom): add domain models and sync service contracts
```

Files changed (3 files, 81 insertions):
- `src/core/iPath.Application/Features/CaseRoom/CaseRoomModels.cs` (new)
- `src/core/iPath.Application/Features/CaseRoom/ICaseRoomSyncService.cs` (new)
- `test/iPath.Test.xUnit2/CaseRoom/CaseRoomModelsTests.cs` (new)

## Self-review findings

- Completeness: all 4 tests from the brief are present verbatim; both source files match the brief byte-for-byte (modulo line endings).
- Quality: namespace is correct (no `Componenents` typo propagated — this namespace is independent). Records are minimal, no extra fields, no comments, file-scoped namespaces.
- Discipline: no extra features, no extra files, no modifications to existing files. YAGNI respected.
- Testing: tests verify construction, both nullability modes of SyncPayload, and nested property access (Viewport.Zoom on the event's payload).
- Build: clean. Pre-existing warnings in sibling projects (Google, EFCore, API) are unrelated and were already present before this commit.

## Concerns

None. The new types are pure additions in a new namespace with no existing dependents; later tasks will wire them into SSE/REST/Blazor layers.