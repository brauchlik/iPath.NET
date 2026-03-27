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
