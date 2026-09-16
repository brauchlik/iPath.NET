# Fix Sub-Item Response Map Bug

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix the bug where sub-item details (Duration, Amount lost) don't appear in Case Description text output.

**Architecture:** The `BuildResponseMap` method in `CaseDescriptionToTextService` flattens the response tree into a dictionary keyed by linkId. It only walks `item` children but LHC Forms nests sub-items inside `answer[].item`. The fix adds a second walk path to also collect items nested under answers.

**Tech Stack:** C#, Firely SDK (Hl7.Fhir.R4), Blazor Server

## Global Constraints

- .NET 10.0 target framework
- Firely SDK v6.4.0 (Hl7.Fhir.R4)
- QuestionnaireItemType enum is nested: `Questionnaire.QuestionnaireItemType`

---

### Task 1: Fix `BuildResponseMap` to walk `answer[].item`

**Files:**
- Modify: `src/core/iPath.Application/Features/Questionnaires/CaseDescriptionToTextService.cs:212-237`

**Interfaces:**
- Produces: Same `Dictionary<string, List<QuestionnaireResponse.ItemComponent>>` but now includes items nested under `answer[].item`

- [ ] **Step 1: Read the current `BuildResponseMap` method**

Current code at lines 212-237:

```csharp
private static Dictionary<string, List<QuestionnaireResponse.ItemComponent>> BuildResponseMap(
    IEnumerable<QuestionnaireResponse.ItemComponent>? items)
{
    var map = new Dictionary<string, List<QuestionnaireResponse.ItemComponent>>(StringComparer.OrdinalIgnoreCase);
    if (items == null) return map;

    void Walk(IEnumerable<QuestionnaireResponse.ItemComponent>? current)
    {
        if (current == null) return;
        foreach (var it in current)
        {
            if (!string.IsNullOrEmpty(it.LinkId))
            {
                if (!map.TryGetValue(it.LinkId, out var list))
                {
                    list = new List<QuestionnaireResponse.ItemComponent>();
                    map[it.LinkId] = list;
                }
                list.Add(it);
            }
            Walk(it.Item);
        }
    }

    Walk(items);
    return map;
}
```

- [ ] **Step 2: Modify `Walk` to also walk `answer[].item`**

Replace the `Walk` method body. After `Walk(it.Item)`, add a loop over `it.Answer` that calls `Walk(a.Item)` for each answer component that has sub-items:

```csharp
void Walk(IEnumerable<QuestionnaireResponse.ItemComponent>? current)
{
    if (current == null) return;
    foreach (var it in current)
    {
        if (!string.IsNullOrEmpty(it.LinkId))
        {
            if (!map.TryGetValue(it.LinkId, out var list))
            {
                list = new List<QuestionnaireResponse.ItemComponent>();
                map[it.LinkId] = list;
            }
            list.Add(it);
        }
        Walk(it.Item);
        if (it.Answer != null)
        {
            foreach (var a in it.Answer)
            {
                Walk(a.Item);
            }
        }
    }
}
```

- [ ] **Step 3: Build and verify**

Run: `dotnet build src/core/iPath.Application/iPath.Application.csproj`
Expected: 0 errors

- [ ] **Step 4: Commit**

```bash
git add src/core/iPath.Application/Features/Questionnaires/CaseDescriptionToTextService.cs
git commit -m "fix: walk answer[].item in BuildResponseMap to collect LHC Forms sub-items"
```
