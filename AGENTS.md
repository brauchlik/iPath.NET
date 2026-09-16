# AGENTS.md

This file contains development guidelines and commands for agentic coding agents working in the iPath.NET repository.

## 🚀 Build, Lint & Test Commands

### Building
```bash
# Build entire solution
dotnet build

# Build specific project
dotnet build src/ui/iPath.Blazor.Server/iPath.Blazor.Server.csproj

# Build with configuration
dotnet build --configuration Release
```

### Running Tests
```bash
# Run all tests
dotnet test

# Run single test by method name
dotnet test --filter "FullyQualifiedName~TestMethodName"

# Run specific test class
dotnet test --filter "IPathSignInManagerTests"

# Run tests in specific project
dotnet test test/iPath.Test.xUnit2/iPath.Test.xUnit2.csproj
```

### Code Quality
```bash
# Treat warnings as errors
dotnet build --warnaserror
```

## 📝 Code Style Guidelines

### Naming Conventions
```csharp
// Classes: PascalCase
public class UserService { }

// Interfaces: Prefix with I
public interface IUserService { }

// Methods/Properties: PascalCase
public async Task<bool> GetUserAsync() { }
public string UserName { get; set; }

// Private fields: _camelCase
private readonly ILogger<UserService> _logger;
```

### Import Organization
```csharp
// Order: System → Microsoft → Project
@using System.ComponentModel.DataAnnotations
@using Microsoft.AspNetCore.Identity
@using iPath.Domain.Authentication
@using iPath.API.Authentication
```

### Razor Components
```razor
@* File naming: PascalCase.razor *@
@* Parameters: PascalCase *@
[Parameter] public bool IsLoading { get; set; }

@* Event handlers: PascalCase *@
private async Task HandleSubmit() { }
```

### Render Modes

The app runs in both `InteractiveServer` and `InteractiveWebAssembly` modes
(`iPathClientConfig:RenderMode` drives the choice). Components must remain
mode-agnostic unless there is a hard reason otherwise.

- ✅ DO: let a component inherit its render mode from its parent (i.e. omit
  `@rendermode ...` entirely). This is the default and works for both Server
  and WASM.
- ❌ DON'T: hard-code `@rendermode InteractiveServerRenderMode(...)` or
  `@rendermode InteractiveWebAssemblyRenderMode(...)` on a shared component.
  Doing so breaks the other mode and produces runtime errors like
  *"render mode 'InteractiveServerRenderMode' is not supported by
  WebAssembly rendering"*.
- ✅ EXCEPTION: Identity pages (login, logout, external callback) and a few
  admin pages may legitimately be Server-only or pure SSR. In those cases,
  the restriction must be justified in a code comment.

### Error Handling
```csharp
// ✅ DO: Use specific exceptions and log
catch (Exception ex)
{
    _logger.LogError(ex, "Error doing {Action}", actionName);
    throw new CustomException("User-friendly message", ex);
}

// ❌ AVOID: Swallowing exceptions
catch (Exception) { }
```

### Logging
```csharp
// ✅ DO: Use structured logging
_logger.LogInformation("User {UserId} logged in", user.Id);

// ❌ AVOID: Sensitive data
_logger.LogInformation("Password: {pwd}", password); // Never!
```

### Security
```csharp
// ✅ DO: Validate inputs
[Required, EmailAddress]
public string Email { get; set; }

// ✅ DO: Use parameterized queries
var users = await _context.Users.Where(u => u.Email == email).ToListAsync();

// ❌ AVOID: SQL injection
var sql = $"SELECT * FROM Users WHERE Email = '{email}'";
```

### Testing
```csharp
// ✅ DO: Descriptive names
[Fact]
public void Login_WithValidCredentials_ShouldReturnSuccess()

// ✅ DO: Arrange-Act-Assert
[Fact]
public void Test()
{
    // Arrange
    var input = CreateInput();
    
    // Act
    var result = Service.Process(input);
    
    // Assert
    Assert.True(result.Success);
}
```

## 🏛️ Architecture

### Project Structure
```
src/
├── core/iPath.Domain/           # Domain entities
├── core/iPath.Application/      # Application services
├── infrastructure/
│   ├── iPath.API/              # API & authentication
│   └── iPath.Database.EFCore/  # EF Core
└── ui/
    ├── iPath.Blazor.Server/    # Blazor Server
    └── iPath.RazorLib/        # Shared components
```

### Dependency Injection
```csharp
// ✅ DO: Constructor injection
public class MyService
{
    private readonly IRepository _repo;
    public MyService(IRepository repo) => _repo = repo;
}

// ❌ AVOID: Service locator
var repo = Ioc.GetService<IRepository>();
```

### Configuration
```csharp
// ✅ DO: Register config classes and use IOptions<T> — no magic strings
// 1. Create a config class (in iPath.Domain.Config)
public class AiSettingsConfig { public const string ConfigName = "AiSettings"; public bool IsEnabled { get; set; } }

// 2. Register in AddIPathAPI or AddPersistance
services.Configure<AiSettingsConfig>(config.GetSection(AiSettingsConfig.ConfigName));
var aiCfg = new AiSettingsConfig();
config.GetSection(AiSettingsConfig.ConfigName).Bind(aiCfg);

// 3. Inject IOptions<T> in handlers/services
public class MyHandler(IOptions<AiSettingsConfig> aiOpts) { ... }

// 4. Or bind locally when needed (for services.AddXxx registration blocks)
var aiCfg = new AiSettingsConfig();
config.GetSection(AiSettingsConfig.ConfigName).Bind(aiCfg);
services.PostConfigure<iPathClientConfig>(c => c.AiEnabled = aiCfg.IsEnabled);

// ❌ AVOID: Magic strings for config section or property names
config.GetSection("AiSettings").GetValue<bool>("IsEnabled");
```

### Async/Await
```csharp
// ✅ DO: Use async all the way
public async Task<Result> ProcessAsync()
{
    return await _service.DoWorkAsync();
}

// ❌ AVOID: Blocking
var result = _service.DoWorkAsync().Result;
```

## 🔒 Security Guidelines

- Never log passwords or sensitive data
- Use parameterized queries
- Validate all inputs
- Use identity password hashing
- Handle exceptions properly with logging

---

## 📋 Special Instructions

Prioritize retrieval-led reasoning over pretrained-knowledge-led reasoning.

### Razor Components & Namespaces
When adding new Razor components in a custom namespace, add the namespace to the folder's `_Imports.razor` file:

```razor
@using iPath.Blazor.Componenents.Admin.Events
```

**Note:** Unresolved component namespaces don't give build errors - they just render as empty. Always verify the `@using` is in place.

### Copilot Guidelines
- Prefer using FHIR R4 for questionnaire to text conversion

### Authentication
- Use `IPathSignInManager` instead of `SignInManager<User>` for new code
- Handle legacy password migration securely
- Log authentication attempts for security auditing
- Use `IPathSignInResult` for consistent error handling

### Project-Specific
- .NET 10.0
- EF Core with multiple database providers
- Blazor Server with MudBlazor
- xUnit testing with FluentAssertions

### Changelog

`CHANGELOG.md` in the repo root is maintained by hand: flat bullets under the
current version heading (`## 0.3`), newest version on top.

- Update it **before committing** - add a one-line bullet for the change you are
  about to commit.
- Keep bullets plain: no sections, no dates, no links.
- If a change is reverted, remove its bullet again.

### UI vs API Boundary

Blazor gives us no technical boundary between UI and server code: the in-process
`DirectApiClient` lets any component call any handler directly, so duplicated
rules creep in silently. Decide deliberately where each piece of logic lives.

- **API / Application (handlers, domain, services):** anything that decides
  whether data is valid, what it means, or what gets persisted. Validation, id
  checks, existence checks, resolution, defaults, invariants, business rules.
- **UI (Razor components, view models):** presentation and interaction only.
  Reading a file into the field it binds to, prefill, formatting, confirmation
  prompts, and surfacing the API's message.

Rules:

- Never re-implement in the UI a rule the API already enforces. Call the API and
  show its response (`resp.ErrorText()`).
- If the UI needs a value the API could also derive (e.g. an id to prefill a
  form), that is a legitimate UI concern - but it must not become a second
  source of truth for the rule.
- One piece of logic, one place. If two components need it, extract it instead
  of copying it.
- When touching an existing flow, check for a second copy before adding to the
  first. The questionnaire FHIR upload exists in both
  `QuestionnaireAdminViewModel.UploadFile` and `DlgEditQuestionnaire.UploadFile`
  - a known duplication to resolve, not to extend.

### EF Core Migrations Workflow

**Rule: Developer runs `dotnet ef` CLI commands, not the AI.**

When code adds/changes entities (new DbSet, new config, property changes):

1. AI checks if a migration is needed (new/modified entity, DbSet, or `IEntityTypeConfiguration`)
2. If yes:
   - Check `dotnet ef --version` is recent — if outdated, remind user to update:
     ```
     dotnet tool update --global dotnet-ef
     ```
   - Show the exact command to run, noting which provider project to run from:
     ```
     cd src\infrastructure\iPath.Database.Sqlite
     dotnet ef migrations add <DescriptiveName> --startup-project ..\..\ui\iPath.Blazor.Server
     ```
   - Remind user: repeat for other providers (Postgres, SqlServer) if needed
3. Developer runs the command, commits the generated migration files

If `dotnet ef migrations add` fails (tool bug):
- AI investigates and suggests workaround (version downgrade, manual migration, etc.)
- Developer applies the fix

### MySQL Double-Encoding (iPath2 Sync Import)

The old iPath2 PHP app connected to MySQL with `Charset=latin1`. When storing UTF-8 bytes like `ö` (`C3 B6`), MySQL treated them as latin1 chars (`Ã¶`), then double-encoded the result into the column's utf8 charset — so the actual DB value is `C3 83 C2 B6`.

**The trick:** `CONVERT(CAST(CONVERT(data USING latin1) AS BINARY) USING utf8mb4)` reverses this in SQL:
1. `CONVERT(data USING latin1)` — tell MySQL to re-interpret the stored string as latin1 chars (recovering the original UTF-8 byte sequence as a latin1 string like `Ã¶`)
2. `CAST(... AS BINARY)` — expose those underlying raw bytes (`C3 B6`)
3. `CONVERT(... USING utf8mb4)` — decode those bytes as UTF-8 into a proper `utf8mb4` string

This lets MySqlConnector/Dapper map the result directly to `string?` without any client-side `byte[] → Encoding.UTF8.GetString()` conversion.

**Reference:** `OldDataService.cs` defines the SQL snippet constants `DataDecode` and `InfoDecode` using this pattern.

### LHC-Forms Bundle (vendored)

`src/ui/iPath.LHCForms/wwwroot/lforms/` holds a vendored LHC-Forms build; `App.razor`
loads `lhc-forms.js` + `fhir/R4/lformsFHIR.min.js` from it.

The upstream release repo named in the original commit (`lhncbc/lforms-versions`) no
longer exists. Take files from the npm package instead, e.g.
`https://cdn.jsdelivr.net/npm/lforms@<version>/dist/lforms/...`:

| our path | package path |
|---|---|
| `lforms/lhc-forms.js` | `dist/lforms/webcomponent/lhc-forms.js` |
| `lforms/styles.css` | `dist/lforms/webcomponent/styles.css` |
| `lforms/assets/lib/zone.min.js` | `dist/lforms/webcomponent/assets/lib/zone.min.js` |
| `lforms/down_arrow_gray_10_10.png`, `lforms/magnifying_glass.png` | `dist/lforms/webcomponent/...` |
| `lforms/fhir/R4/lformsFHIR.min.js` | `dist/lforms/fhir/R4/lformsFHIR.min.js` |

**Always strip the `@layer lforms{...}` wrappers from `styles.css` after upgrading.**
Since 43.0.0 upstream wraps its vendor CSS in a cascade layer. MudBlazor ships an
*unlayered* preflight containing `*{border-width:0}`, and unlayered declarations beat
layered ones regardless of specificity - so every LForms border silently disappears
(the boolean radio buttons, for example, render as invisible white-on-white outlines).
There are ~10 top-level `@layer lforms{` blocks; remove each prefix and its matching
`}`, leaving the inner rules (with their `:where(.lhc-form)` scoping) unlayered.
Verify afterwards: brace count still balanced, no `@layer` left, radios visible again.

Current version: 44.0.0 (upgraded from 38.7.2). The `_codingsEqual` null-guard in
`wwwroot/lhcformsJsInterop.js` is still required - 44.0.0 does not fix it upstream.
