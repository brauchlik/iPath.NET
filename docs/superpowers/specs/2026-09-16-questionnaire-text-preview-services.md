# Per-Questionnaire Text Preview Services — Design

**Date:** 2026-09-16
**Status:** Implementation
**Owner:** Kurt / iPath.NET

## 1. Goal

Allow per-questionnaire configuration of the text preview service used to generate
human-readable text from `QuestionnaireResponse` resources. Admins select a
service variant per questionnaire; the system resolves the correct service at
runtime via keyed dependency injection.

## 2. Context (as-is)

- `IQuestionnaireToTextService.CreateText(QuestionnaireResponse, Questionnaire)`
  generates text previews displayed in the UI and stored in `GeneratedText`.
- Two existing implementations: `GenericQuestionnaireToListTextService` (HTML
  list) and `GenericQuestionnaireToCvsTextService` (CSV pairs).
- `QuestionnaireSettings` (stored as JSON in DB) had `Filename` and
  `BodySiteFilter` only — no way to configure the text service per questionnaire.
- `UpdateServiceRequestHandler` used a single injected `IQuestionnaireToTextService`
  for all questionnaires.

## 3. Design

### 3.1 Registry

`IQuestionnaireToTextServiceRegistry` provides metadata about available services:

```csharp
public record TextPreviewServiceInfo(string Key, string DisplayName, string? Description = null);

public interface IQuestionnaireToTextServiceRegistry
{
    IReadOnlyList<TextPreviewServiceInfo> GetAll();
    TextPreviewServiceInfo? GetDefault();
}
```

`QuestionnaireToTextServiceRegistry` implements this with a hardcoded list that
matches the DI registrations.

### 3.2 Keyed Services

All `IQuestionnaireToTextService` implementations are registered as **keyed
transient services** with human-readable keys:

| Key | Class | Description |
|-----|-------|-------------|
| `Default List` | `GenericQuestionnaireToListTextService` | HTML list format (default) |
| `CSV` | `GenericQuestionnaireToCvsTextService` | Comma-separated pairs |
| `Case Description (Compact)` | `CaseDescriptionToTextService` | Grouped by section, sub-items in `[brackets]` |
| `Case Description (Expanded)` | `CaseDescriptionToTextService` | Grouped by section, sub-items on new lines |

The `CaseDescriptionToTextService` accepts a `CaseDescriptionOutputMode` enum
(`Compact` or `Expanded`) via constructor parameter. Factory delegates handle
the two registrations.

### 3.3 Settings

`QuestionnaireSettings.TextPreviewService` stores the selected key (string).
`null` or empty means "use default" (`Default List`).

No DB migration needed — Settings is a JSON column; new field defaults to `null`.

### 3.4 Resolution

In `UpdateServiceRequestHandler` and the admin preview:

```csharp
var serviceKey = settings?.TextPreviewService;
var q2t = string.IsNullOrEmpty(serviceKey)
    ? sp.GetRequiredKeyedService<IQuestionnaireToTextService>("Default List")
    : sp.GetKeyedService<IQuestionnaireToTextService>(serviceKey)
      ?? sp.GetRequiredKeyedService<IQuestionnaireToTextService>("Default List");
```

Graceful fallback: if the key doesn't match any registered service, use default.

### 3.5 Cache Extension

`QuestionnaireCacheServer` now caches `QuestionnaireSettings` alongside the
deserialized FHIR `Questionnaire`, exposed via `GetSettingsAsync()`.

## 4. CaseDescriptionToTextService Algorithm

1. Build a `linkId → response items` map by walking the response tree.
2. Walk the Questionnaire structure **group by group** (top-level `type: "group"`).
3. For each group:
   - Collect boolean items with value `true` as their text label.
   - For booleans with sub-items (e.g. abdominal pain → location + duration),
     collect sub-item values and append in brackets.
   - For choice items, collect selected answer values.
   - Skip groups with no answered items.
4. Output format:
   - **Compact:** `Symptoms: Dysphagia, Abdominal pain [Upper abdomen (left), 3 months], Fever`
   - **Expanded:** one line per group + indented sub-item lines with `→` arrow.

### Edge Cases

- **Vomiting blood** (boolean sub-item): rendered as `Vomiting [Vomiting blood]`
  when both are true.
- **Quantity values**: rendered as `{value} {unit}` (e.g. `5 kg`, `3 months`).
- **Nested enableWhen**: only items present in the response map are included,
  so disabled items are automatically excluded.

## 5. Files

### New Files
- `src/core/iPath.Application/Features/Questionnaires/IQuestionnaireToTextServiceRegistry.cs`
- `src/core/iPath.Application/Features/Questionnaires/QuestionnaireToTextServiceRegistry.cs`
- `src/core/iPath.Application/Features/Questionnaires/CaseDescriptionToTextService.cs`
- `docs/superpowers/specs/2026-09-16-questionnaire-text-preview-services.md`

### Modified Files
- `src/core/iPath.Domain/Entities/Questionnaires/QuestionnaireSettings.cs` — added `TextPreviewService`
- `src/core/iPath.Application/Services/QuestionnaireCacheServer.cs` — added `GetSettingsAsync()`
- `src/infrastructure/iPath.API/APIServicesRegistration.cs` — keyed service registrations
- `src/infrastructure/iPath.Database.EFCore/FeatureHandlers/ServiceRequests/Commands/UpdateServiceRequestHandler.cs` — keyed resolution
- `src/ui/iPath.RazorLib/Admin/Questionnaires/QuestionnairesAdminPage.razor` — dropdown in Settings tab
- `src/ui/iPath.RazorLib/Admin/Questionnaires/QuestionnaireAdminViewModel.cs` — preview uses keyed service

## 6. Adding a New Service

To add a new text preview service:

1. Create a class implementing `IQuestionnaireToTextService`.
2. Register it in `APIServicesRegistration.cs` with a unique key:
   ```csharp
   services.AddKeyedTransient<IQuestionnaireToTextService, MyNewService>("My Service Name");
   ```
3. Add an entry in `QuestionnaireToTextServiceRegistry.Services`:
   ```csharp
   new("My Service Name", "Display Name", "Description shown in admin UI")
   ```
4. The admin dropdown populates automatically from the registry.
