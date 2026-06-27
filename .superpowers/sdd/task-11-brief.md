### Task 11: Final build + manual test plan

**Files:** none — verification only.

- [ ] **Step 1: Run all tests**

Run: `dotnet test test/iPath.Test.xUnit2/iPath.Test.xUnit2.csproj`
Expected: All CaseRoom tests PASS (Task 1: 4, Task 2: 2, Task 3: 7, Task 5: 1, Task 6: 3, Task 7: 2 = 19 tests). No previously-passing tests regressed.

- [ ] **Step 2: Build the solution in Release configuration**

Run: `dotnet build --configuration Release`
Expected: Build succeeded with no warnings above the existing baseline.

- [ ] **Step 3: Manual test plan**

Run the app in server mode (e.g., `dotnet run --project src/ui/iPath.Blazor.Server/iPath.Blazor.Server.csproj`).

Test 1 (single-user smoke):
- Navigate to a ServiceRequest that has at least one .vsi image and at least 1 .dzi tile source
- Navigate to `/request/{id}/caseroom`
- Verify: page loads, OSD renders, prev/next works, pan/zoom smooth
- Verify: "1 viewing" chip shows your own presence

Test 2 (two-browser sync, server mode):
- Open the same `/request/{id}/caseroom` in two browsers (e.g., one normal window + one private window, signed in as two different users)
- In window A: pan and zoom the slide
- Verify: window B's OSD follows the same viewport within ~150ms
- In window A: click "Next" to change document
- Verify: window B's OSD switches to the same document
- In window A: pan again
- Verify: window B follows without echo loops

Test 3 (leave cleanup):
- Close one browser tab
- Verify: the remaining window shows "1 viewing" (participant count decremented within 30s)
- Close all tabs
- Re-open the same case → session recreates cleanly

Test 4 (badge):
- Open `/request/{id}/caseroom` in one window
- Open `/request/{id}` in a different window
- Verify: "X in CaseRoom" chip is visible on the ServiceRequest page with the correct count

- [ ] **Step 4: Commit final test results as a Markdown report (optional)**

If manual tests pass, write a brief note to `docs/superpowers/plans/2026-06-27-caseroom-test-results.md` and commit:

```bash
git add docs/superpowers/plans/
git commit -m "docs(caseroom): manual test results"
```

- [ ] **Step 5: Final commit (if any tests changed during integration)**

```bash
git status
# If anything uncommitted:
git add -A
git commit -m "test(caseroom): integration polish"
```

