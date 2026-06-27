### Task 10: "CaseRoom active" badge on ServiceRequest page

**Files:**
- Modify: `src/ui/iPath.RazorLib/ServiceRequests/ServiceRequestPage.razor`

**Interfaces:**
- Consumes: `IPathApi.GetCaseRoomStatus` (Refit) or via server direct, polling endpoint added in Task 4
- Produces: a chip linking to `/request/{id}/caseroom` when a room is active

- [ ] **Step 1: Read the current ServiceRequestPage.razor**

Run: identify the file by reading `src/ui/iPath.RazorLib/ServiceRequests/ServiceRequestPage.razor` (it was modified recently per git log).

- [ ] **Step 2: Add room-status polling and badge**

Modify `src/ui/iPath.RazorLib/ServiceRequests/ServiceRequestPage.razor`. After the existing toolbar/header area, add a chip when a room is active:

```razor
@if (CaseRoomActive)
{
    <MudChip Color="Color.Success" Size="Size.Small" Variant="Variant.Filled"
             Href="@($"request/{id}/caseroom")" Class="ma-1"
             Icon="@Icons.Material.Filled.Group">
        @CaseRoomParticipantCount in CaseRoom
    </MudChip>
}
```

In the `@code` block of the page (or its code-behind if there is one), add:

```csharp
private bool CaseRoomActive;
private int CaseRoomParticipantCount;

protected override async Task OnAfterRenderAsync(bool firstRender)
{
    if (firstRender && vm.SelectedRequest is not null)
    {
        try
        {
            var resp = await api.GetCaseRoomStatus(vm.SelectedRequest.Id);
            if (resp.IsSuccessful && resp.Content is not null)
            {
                CaseRoomActive = resp.Content.IsActive;
                CaseRoomParticipantCount = resp.Content.ParticipantCount;
                StateHasChanged();
            }
        }
        catch { /* non-fatal: room status is informational */ }
    }
}
```

> **Note:** If the page already injects `IPathApi api` via the ViewModel pattern (which it does via `vm`), call it through the ViewModel by adding a helper method `ReloadCaseRoomStatusAsync()` on `ServiceRequestViewModel`, OR inject `IPathApi` directly into the page for this one call. Prefer injecting `IPathApi` directly to keep the change minimal. Verify the existing `ServiceRequestPage.razor` structure and adapt to existing patterns.

- [ ] **Step 3: Build**

Run: `dotnet build`
Expected: succeeded.

- [ ] **Step 4: Commit**

```bash
git add src/ui/iPath.RazorLib/ServiceRequests/ServiceRequestPage.razor
git commit -m "feat(caseroom): show CaseRoom active badge on ServiceRequest page"
```

