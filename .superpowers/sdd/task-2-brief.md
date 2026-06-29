## Task 2: Models

**Files:**
- Create: `src/VsiConverter/VsiConverter.UI/Models/AvailableSeries.cs`
- Create: `src/VsiConverter/VsiConverter.UI/Models/ConversionStatus.cs`

**Interfaces:**
- Consumes: Task 1 (project scaffolding — buildable project exists)
- Produces: `AvailableSeries` record, `ConversionStatus` enum used by all later tasks

- [ ] **Step 1: Create AvailableSeries.cs**
  File: `src/VsiConverter/VsiConverter.UI/Models/AvailableSeries.cs`

```csharp
namespace VsiConverter.UI.Models;

public record AvailableSeries(
    int Index,
    int Width,
    int Height,
    double PixelSizeX,
    string? Description)
{
    public override string ToString()
        => $"Series {Index}: {Width}x{Height}{(Description is not null ? $" ({Description})" : "")}";
}
```

- [ ] **Step 2: Create ConversionStatus.cs**
  File: `src/VsiConverter/VsiConverter.UI/Models/ConversionStatus.cs`

```csharp
namespace VsiConverter.UI.Models;

public enum ConversionStatus
{
    Queued,
    CheckingCompanion,
    DetectingSeries,
    Converting,
    Zipping,
    Completed,
    Failed,
    Cancelled
}
```

- [ ] **Step 3: Verify build**
  Run: `dotnet build src/VsiConverter/VsiConverter.UI/VsiConverter.UI.csproj`
  Expected: Build succeeds with 0 warnings, 0 errors

- [ ] **Step 4: Commit**
  ```bash
  git add src/VsiConverter/
  git commit -m "feat: add AvailableSeries model and ConversionStatus enum"
  ```
