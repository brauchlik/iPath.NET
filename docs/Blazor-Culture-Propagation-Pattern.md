# Blazor Culture Propagation Pattern

A reusable pattern for propagating the server-side culture (from cookie/`RequestLocalizationMiddleware`) to the WASM client in hybrid Blazor apps, working across all render modes and prerender settings.

## The Problem

In Blazor Web Apps with interactive WebAssembly (WASM) or Auto render mode:

| Scenario | Server Culture | WASM Client Culture | Result |
|---|---|---|---|
| Prerender ON | ✅ Correct (from middleware + cookie) | ❌ Browser default (`navigator.language`) | Language flashes then resets |
| Prerender OFF | N/A (no server render) | ❌ Browser default | Wrong language from start |
| Server mode | ✅ Works fine | N/A | ✅ |
| Authenticated (prerender disabled) | ❌ No prerender = no transfer | ❌ Browser default | Wrong language |

**Root cause:** WASM has no `RequestLocalizationMiddleware`. `CultureInfo.CurrentUICulture` defaults to the browser's `navigator.language`, ignoring the `.AspNetCore.Culture` cookie the server set.

## The Pattern

Three components work together:

```
Browser ←→ Server (sets cookie) ←→ API Endpoint (reads cookie)
                ↓
          WASM Program.cs (fetches endpoint → sets culture BEFORE Build)
                ↓
          Preload translations for correct culture
```

### 1. Server: Culture Query Endpoint

A lightweight endpoint that reads the current request's culture (from the middleware) and returns it:

```csharp
app.MapGet("/api/localization/current", (HttpContext httpContext, IOptions<LocalizationSettings> l10n) =>
{
    var feature = httpContext.Features.Get<IRequestCultureFeature>();
    var locale = feature?.RequestCulture.UICulture.TwoLetterISOLanguageName ?? "en";
    
    return Results.Ok(new 
    { 
        currentCulture = locale,
        supportedCultures = l10n.Value.SupportedCultures,
        cultureDisplayNames = l10n.Value.CultureDisplayNames
    });
}).AllowAnonymous();
```

**Key points:**
- `AllowAnonymous()` — called before auth resolves
- Uses `IRequestCultureFeature` (populated by `RequestLocalizationMiddleware`) — no manual cookie parsing
- Also returns `supportedCultures` / `cultureDisplayNames` so WASM knows the full list

### 2. WASM Program.cs: Culture Fetch Before Build

Fetch the culture **before** building the app, and set it for the entire app lifetime:

```csharp
// fetch per-session culture from server (reads .AspNetCore.Culture cookie)
string currentCulture = "en";
try
{
    using var http = new HttpClient()
    {
        BaseAddress = new Uri(builder.HostEnvironment.BaseAddress),
        Timeout = TimeSpan.FromSeconds(5)
    };

    using var cultureResp = await http.GetAsync("api/localization/current");
    using var cultureStream = await cultureResp.Content.ReadAsStreamAsync();
    var cultureData = await JsonSerializer.DeserializeAsync<CultureResponse>(cultureStream, 
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    currentCulture = cultureData?.CurrentCulture ?? "en";
}
catch (Exception ex)
{
    Console.WriteLine($"Error fetching culture: {ex}");
}

// set culture for ENTIRE app lifetime BEFORE building
var ci = CultureInfo.GetCultureInfo(currentCulture);
CultureInfo.DefaultThreadCurrentCulture = ci;
CultureInfo.DefaultThreadCurrentUICulture = ci;

// ... register services, Build(), then preload translations for the correct culture
var srvLoc = app.Services.GetRequiredService<ITranslationLoader>();
await srvLoc.LoadTranslationData(ci.TwoLetterISOLanguageName);
```

**Critical timing:** The culture must be set **before** `builder.Build()` and **before** any component initializes or translations are preloaded.

### 3. Wasm Client Project: Globalization Data

Set `<BlazorWebAssemblyLoadAllGlobalizationData>true</BlazorWebAssemblyLoadAllGlobalizationData>` in the `.csproj` of the WASM project. Without this, changing the culture dynamically at startup throws:

```
Blazor detected a change in the application's culture that is not supported
```

### 4. Language Switch Flow

```
User clicks "Deutsch" in language menu
    → MainLayout.SwitchLanguage("de")
    → NavigateTo("/api/localization/set?culture=de&redirectUri=...", forceLoad: true)
    → Server sets .AspNetCore.Culture cookie, redirects back
    → Browser loads page fresh
    → WASM Program.cs calls /api/localization/current
    → Gets "de" from cookie (via middleware)
    → Sets DefaultThreadCurrentUICulture = "de"
    → Preloads German translations
```

## Coverage Matrix

| Render Mode | Prerender | Authenticated | Works? | Why |
|---|---|---|---|---|
| InteractiveServer | both | any | ✅ | Server middleware handles it natively |
| InteractiveWebAssembly | true | no | ✅ | Endpoint returns cookie culture on load |
| InteractiveWebAssembly | false | yes | ✅ | Same — endpoint called on every fresh load |
| InteractiveAuto | both | any | ✅ | Same — mode-independent |

## Prerequisites

1. **Server**: `RequestLocalizationMiddleware` configured with supported cultures + cookie provider
2. **Cookie**: `.AspNetCore.Culture` set on language change
3. **WASM csproj**: `<BlazorWebAssemblyLoadAllGlobalizationData>true</BlazorWebAssemblyLoadAllGlobalizationData>`

## Nuances & Pitfalls

- **`PersistAsJson` / `PersistentComponentState`** ❌ Do NOT use these for culture. `PersistAsJson` can only be called inside `RegisterOnPersisting` callbacks, and this approach only works during prerender. The endpoint approach is simpler and works in all modes.
- **`DefaultThreadCurrentUICulture` vs `CurrentUICulture`** — Setting `DefaultThreadCurrent*` alone does NOT change the current thread. Use `CultureInfo.CurrentCulture` and `CultureInfo.CurrentUICulture` (setters) for the current thread, OR set `DefaultThreadCurrent*` **before** the app starts (in `Program.cs`, before `Build()`).
- **JS Interop alternative**: Microsoft docs show reading from `localStorage` via JS. The endpoint approach avoids JS dependency and uses the existing auth/cookie infrastructure.
- **.NET 10+ built-in**: `WebAssemblyComponentsOptions.UseCultureFromServer` (default `true`) automatically propagates culture during prerender, but only during prerender. Our endpoint approach is more comprehensive.
