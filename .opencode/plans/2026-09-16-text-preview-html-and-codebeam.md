# Text Preview HTML + CodeBeam MudCodeViewer

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Move CaseDescriptionToTextService output from plain text to light HTML, add CodeBeam.MudBlazor.Extensions for MudCodeViewer on the JSON toggle, and fix the sub-item response map bug.

**Architecture:** The service produces HTML strings (`<strong>` for groups, `<br/>` for linebreaks, `&nbsp;` for indentation). The preview UI renders via `MarkupString`. JSON display uses `MudCodeViewer` from CodeBeam.MudBlazor.Extensions.Code (Prism.js syntax highlighting).

**Tech Stack:** C#, .NET 10.0, Firely SDK v6.4.0, MudBlazor 9.9.0, CodeBeam.MudBlazor.Extensions 9.1.0

## Global Constraints

- .NET 10.0 target framework
- MudBlazor 9.9.0
- Firely SDK v6.4.0 (Hl7.Fhir.R4)
- QuestionnaireItemType is nested: `Questionnaire.QuestionnaireItemType`
- `GeneratedText` is consumed as `MarkupString` (HTML) in `ServiceRequestQuestionnaire.razor` and `EmailNotificationPreview.cs`

## File Map

| File | Change |
|---|---|
| `src/core/iPath.Application/Features/Questionnaires/CaseDescriptionToTextService.cs` | Fix BuildResponseMap + HTML output |
| `src/ui/iPath.RazorLib/Admin/Questionnaires/QuestionnairesAdminPage.razor` | Render HTML via MarkupString |
| `src/ui/iPath.RazorLib/iPath.Blazor.Componenents.csproj` | Add CodeBeam packages |
| `src/ui/iPath.Blazor.Server/Components/App.razor` | Add CodeBeam CSS/JS static assets |
| `src/ui/iPath.Blazor.Server/Program.cs` | Add `AddMudExtensions()` |
| `src/ui/iPath.RazorLib/_Imports.razor` | Add `@using MudExtensions` |
| `src/ui/iPath.RazorLib/Admin/Questionnaires/QuestionnairesAdminPage.razor` | Replace `<pre>` with `MudCodeViewer` |

---

### Task 1: Fix `BuildResponseMap` sub-item bug

**Files:**
- Modify: `src/core/iPath.Application/Features/Questionnaires/CaseDescriptionToTextService.cs:218-241`

**Interfaces:**
- Produces: Same `Dictionary<string, List<QuestionnaireResponse.ItemComponent>>` but now includes items nested under `answer[].item`

- [ ] **Step 1: Modify `Walk` to also walk `answer[].item`**

In the `Walk` method inside `BuildResponseMap`, after the line `Walk(it.Item);` (line 232), add:

```csharp
if (it.Answer != null)
{
    foreach (var a in it.Answer)
    {
        Walk(a.Item);
    }
}
```

- [ ] **Step 2: Build and verify**

Run: `dotnet build src/core/iPath.Application/iPath.Application.csproj`
Expected: 0 errors

---

### Task 2: `CaseDescriptionToTextService` → Light HTML output

**Files:**
- Modify: `src/core/iPath.Application/Features/Questionnaires/CaseDescriptionToTextService.cs`

**Interfaces:**
- Produces: HTML string from `CreateText()` instead of plain text
- Consumers: `ServiceRequestQuestionnaire.razor` (already `MarkupString`), `EmailNotificationPreview.cs` (already appends `<br />`), Preview UI (will switch to `MarkupString`)

- [ ] **Step 1: Change `CreateText` group output to HTML**

Replace line 33:
```csharp
sb.AppendLine($"{groupLabel}: {entries}");
```
With:
```csharp
sb.AppendLine($"<strong>{groupLabel}:</strong> {entries}");
```

- [ ] **Step 2: Change expanded mode sub-lines to HTML**

Replace lines 38-40 (the expanded sub-line append):
```csharp
sb.AppendLine($"  {subLine}");
```
With:
```csharp
sb.AppendLine($"&nbsp;&nbsp;{subLine}<br/>");
```

- [ ] **Step 3: Change `ProcessBooleanItem` expanded sub-line to HTML**

Replace line 118:
```csharp
subLines.Add($"{label} \u2192 {subCombined}");
```
With:
```csharp
subLines.Add($"{label} \u2192 {subCombined}<br/>");
```

- [ ] **Step 4: Build and verify**

Run: `dotnet build src/core/iPath.Application/iPath.Application.csproj`
Expected: 0 errors

---

### Task 3: Preview UI — render HTML

**Files:**
- Modify: `src/ui/iPath.RazorLib/Admin/Questionnaires/QuestionnairesAdminPage.razor`

- [ ] **Step 1: Change text output to MarkupString**

Replace line 111:
```razor
<div style="white-space: pre-wrap;">@vm.PreviewText</div>
```
With:
```razor
@((MarkupString)vm.PreviewText)
```

- [ ] **Step 2: Build and verify**

Run: `dotnet build`
Expected: 0 errors

---

### Task 4: Add CodeBeam.MudBlazor.Extensions packages

**Files:**
- Modify: `src/ui/iPath.RazorLib/iPath.Blazor.Componenents.csproj`
- Modify: `src/ui/iPath.Blazor.Server/Components/App.razor`
- Modify: `src/ui/iPath.Blazor.Server/Program.cs` (or equivalent service registration)
- Modify: `src/ui/iPath.RazorLib/_Imports.razor`

- [ ] **Step 1: Add NuGet packages to RazorLib**

Add to `iPath.Blazor.Componenents.csproj`:
```xml
<PackageReference Include="CodeBeam.MudBlazor.Extensions" Version="9.1.0" />
<PackageReference Include="CodeBeam.MudBlazor.Extensions.Code" Version="9.1.0" />
```

- [ ] **Step 2: Add CSS/JS static assets to App.razor**

In the `<head>` section, add:
```html
<link href="_content/CodeBeam.MudBlazor.Extensions/MudExtensions.min.css" rel="stylesheet" />
<link href="_content/CodeBeam.MudBlazor.Extensions.Code/prism/prism.min.css" rel="stylesheet" />
```

In the `<body>` section, add (after MudBlazor JS):
```html
<script src="_content/CodeBeam.MudBlazor.Extensions/MudExtensions.min.js"></script>
<script src="_content/CodeBeam.MudBlazor.Extensions.Code/prism/prism.min.js"></script>
```

- [ ] **Step 3: Register MudExtensions service**

In `Program.cs` (or wherever `AddMudBlazor()` is called), add:
```csharp
using MudExtensions.Services;
builder.Services.AddMudExtensions();
```

- [ ] **Step 4: Add using to _Imports.razor**

Add to `src/ui/iPath.RazorLib/_Imports.razor`:
```razor
@using MudExtensions
```

- [ ] **Step 5: Build and verify**

Run: `dotnet build`
Expected: 0 errors

---

### Task 5: Replace `<pre>` with MudCodeViewer

**Files:**
- Modify: `src/ui/iPath.RazorLib/Admin/Questionnaires/QuestionnairesAdminPage.razor`

- [ ] **Step 1: Replace pre tag with MudCodeViewer**

Replace the `<pre>` block (lines ~121-123):
```razor
<pre style="white-space: pre-wrap; font-size: 0.85rem;">@vm.PreviewResponseJson</pre>
```
With:
```razor
<MudCodeViewer Code="@vm.PreviewResponseJson" Language="json"
               ShowLineNumbers="true" Theme="MudBlazor.Extensions.Code.Themes.CodeTheme.Dark" />
```

Note: The exact theme name and Language parameter may need adjustment. Check CodeBeam docs or use `"json"` for language.

- [ ] **Step 2: Build and verify**

Run: `dotnet build`
Expected: 0 errors

---

### Task 6: Final verification

- [ ] **Step 1: Full solution build**

Run: `dotnet build`
Expected: 0 errors

- [ ] **Step 2: Manual test checklist**

- Open admin questionnaires page
- Select a questionnaire with sub-items (e.g., hematology-lymph)
- Settings tab: verify "Text Preview Mode" at bottom with Outlined variant
- Preview tab: verify "Text Preview" header, mode selector, Generate button
- Fill in form, click Generate
- Verify compact mode shows `<strong>` groups (should render bold)
- Switch to expanded mode, verify sub-item details show with `→` and indentation
- Switch to "Response JSON" toggle, verify MudCodeViewer shows syntax-highlighted JSON
