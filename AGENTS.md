# AGENTS.md

This file contains development guidelines and commands for agentic coding agents working in the iPath.NET repository.

## 🚀 Build, Lint & Test Commands

### 🏗️ Building the Solution

#### Full Solution Build
```bash
# Build entire solution (recommended for PRs)
dotnet build

# Build with specific configuration
dotnet build --configuration Release
dotnet build --configuration Debug

# Build specific project
dotnet build src/ui/iPath.Blazor.Server/iPath.Blazor.Server.csproj
dotnet build src/infrastructure/iPath.API/iPath.API.csproj
```

#### Clean Build
```bash
# Clean then build
dotnet clean && dotnet build

# Clean specific project
dotnet clean src/ui/iPath.Blazor.Server/iPath.Blazor.Server.csproj
```

### 🧪 Running Tests

#### All Tests
```bash
# Run all tests in solution
dotnet test

# Run with detailed output
dotnet test --verbosity normal

# Run with configuration
dotnet test --configuration Release
```

#### Specific Test Categories
```bash
# Run authentication tests only
dotnet test --filter "FullyQualifiedName~Authentication"

# Run specific test class
dotnet test --filter "IPathSignInManagerTests"
dotnet test --filter "AuthenticationFlowTests"

# Run single test method
dotnet test --filter "IPathSignInManagerTests.CreateMD5Hash_ShouldBeConsistent"
```

#### Test Results & Coverage
```bash
# Run tests with coverage (if configured)
dotnet test --collect:"XPlat Code Coverage"

# Run specific project tests
dotnet test test/iPath.Test.xUnit2/iPath.Test.xUnit2.csproj

# Run tests and output to file
dotnet test --logger "console;verbosity=detailed" --results-directory TestResults
```

### 🔍 Code Quality & Analysis

#### Linting (if available)
```bash
# Check for style violations (if analyzers are configured)
dotnet build --verbosity diagnostic

# Run specific analyzers
dotnet build --verbosity diagnostic --no-restore
```

#### Warnings as Errors
```bash
# Treat warnings as errors (for CI/CD)
dotnet build --warnaserror

# Specific warning codes as errors
dotnet build --warnaserror:CS1591
```

## 📝 Code Style Guidelines

### 🏛️ Architecture & Project Structure

#### Solution Structure
```
src/
├── core/iPath.Domain/           # Domain entities and interfaces
├── core/iPath.Application/        # Application services and features  
├── infrastructure/
│   ├── iPath.API/              # API services and authentication
│   ├── iPath.Database.EFCore/   # EF Core implementation
│   └── [DatabaseProviders]/     # Database-specific implementations
├── ui/
│   ├── iPath.Blazor.Server/     # Server-side Blazor app
│   ├── iPath.Blazor.Client/     # Client-side Blazor app
│   └── iPath.RazorLib/        # Shared UI components
└── test/iPath.Test.xUnit2/       # Unit and integration tests
```

#### Dependency Injection Patterns
```csharp
// ✅ DO: Use constructor injection
public class MyService : IMyService
{
    private readonly ILogger<MyService> _logger;
    private readonly IRepository _repository;
    
    public MyService(ILogger<MyService> logger, IRepository repository)
    {
        _logger = logger;
        _repository = repository;
    }
}

// ❌ AVOID: Service locator pattern
public class MyService
{
    public void DoWork()
    {
        var service = Ioc.GetService<IRepository>(); // Bad practice
    }
}
```

#### Async/Await Patterns
```csharp
// ✅ DO: Use ConfigureAwait(false) in library code
public async Task<string> GetDataAsync()
{
    var data = await _repository.GetDataAsync().ConfigureAwait(false);
    return data;
}

// ✅ DO: Use async all the way down
public async Task<string> ProcessDataAsync()
{
    var result = await _service.ProcessAsync();
    return result;
}

// ❌ AVOID: Async/await deadlocks
public async Task<string> GetDataAsync()
{
    var data = _repository.GetDataAsync().Result; // Can cause deadlocks
    return data;
}
```

### 📋 Naming Conventions

#### C# Naming
```csharp
// ✅ Classes: PascalCase
public class AuthenticationService { }

// ✅ Interfaces: Prefix with I
public interface IAuthenticationService { }

// ✅ Methods: PascalCase
public async Task<bool> AuthenticateUserAsync() { }

// ✅ Properties: PascalCase
public string UserName { get; set; }

// ✅ Private fields: _camelCase with underscore
private readonly ILogger<AuthService> _logger;

// ✅ Constants: PascalCase or UPPER_CASE
public const int MAX_LOGIN_ATTEMPTS = 5;

// ❌ AVOID: Hungarian notation
public string strUserName; // Don't use
public int intUserId; // Don't use
```

#### Razor Component Naming
```csharp
// ✅ Component files: PascalCase.razor
Login.razor
UserProfile.razor
AuthenticationFlow.razor

// ✅ Component class names: PascalCase without suffix
public partial class Login : ComponentBase { } // Not LoginComponent

// ✅ Parameter names: PascalCase
[Parameter] public string UserName { get; set; }
[Parameter] public bool IsLoading { get; set; }

// ✅ Event handlers: PascalCase
private async Task OnClick() { }
private async Task HandleLoginSubmit() { }
```

### 📚 Import Organization

#### Using Statement Order
```csharp
// ✅ DO: Organize imports logically
@using System.ComponentModel.DataAnnotations
@using Microsoft.AspNetCore.Identity
@using Microsoft.AspNetCore.WebUtilities
@using iPath.Domain.Authentication
@using iPath.API.Authentication

// ✅ DO: System imports first, then Microsoft, then project
@using System;
@using System.Text;
@using Microsoft.AspNetCore.Identity;
@using Microsoft.AspNetCore.WebUtilities;
@using iPath.Domain;
@using iPath.API;

// ❌ AVOID: Unordered or duplicate imports
```

#### Namespace Imports
```csharp
// ✅ DO: Specific imports
using iPath.Domain.Authentication;
using iPath.API.Authentication;

// ❌ AVOID: Wildcard imports
using iPath.Domain.*; // Avoid when possible
```

### 🎨 Error Handling

#### Exception Handling Patterns
```csharp
// ✅ DO: Use specific exception types
if (user == null)
{
    throw new UserNotFoundException($"User with ID {userId} not found");
}

// ✅ DO: Log exceptions properly
catch (Exception ex)
{
    _logger.LogError(ex, "Error authenticating user {username}", username);
    throw new AuthenticationException("Authentication failed", ex);
}

// ✅ DO: Use Result pattern for operations without exceptions
public async Task<IPathSignInResult> SignInAsync(string username, string password)
{
    try
    {
        var result = await _signInManager.PasswordSignInAsync(username, password);
        return IPathSignInResult.Success(result.User);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Sign in failed for {username}", username);
        return IPathSignInResult.Failed("Authentication failed");
    }
}

// ❌ AVOID: Catch-all exception handling
catch (Exception) { } // At least log the exception
catch { } // Never swallow exceptions silently
```

#### Logging Best Practices
```csharp
// ✅ DO: Use structured logging
_logger.LogInformation("User {UserId} authenticated successfully", user.Id);
_logger.LogWarning("Failed login attempt for {Username} from {IP}", username, ipAddress);

// ✅ DO: Log at appropriate levels
_logger.LogDebug("Debug information");
_logger.LogInformation("Important information");
_logger.LogWarning("Warning conditions");
_logger.LogError("Error conditions");

// ✅ DO: Include context in logs
_logger.LogInformation("User authentication completed: {UserId}, {Username}, {Timestamp}", 
    user.Id, user.UserName, DateTime.UtcNow);

// ❌ AVOID: Logging sensitive data
_logger.LogInformation("User logged in with password: {password}", password); // Never log passwords
_logger.LogInformation("User credit card: {cardNumber}", cardNumber); // Never log sensitive data
```

### 🔒 Security Guidelines

#### Authentication & Authorization
```csharp
// ✅ DO: Use proper password hashing
var result = await _userManager.CreateAsync(user, password);

// ✅ DO: Validate inputs thoroughly
var emailAttr = new EmailAddressAttribute();
if (!emailAttr.IsValid(email))
{
    return IPathSignInResult.Failed("Invalid email format");
}

// ✅ DO: Use constant-time comparisons for passwords
if (!VerifyPasswordHash(providedHash, storedHash))
{
    return IPathSignInResult.Failed("Invalid credentials");
}

// ❌ AVOID: Timing attacks through error messages
if (userNotFound)
{
    return IPathSignInResult.Failed("Invalid username or password"); // Don't specify which is wrong
}
```

#### Input Validation
```csharp
// ✅ DO: Validate all inputs
[Required]
[StringLength(100, MinimumLength = 3)]
public string UserName { get; set; }

// ✅ DO: Sanitize user inputs
var sanitizedInput = WebUtility.HtmlEncode(userInput);

// ✅ DO: Use parameterized queries
var users = await _context.Users
    .Where(u => u.Email == email) // Not string interpolation
    .ToListAsync();

// ❌ AVOID: SQL injection
var sql = $"SELECT * FROM Users WHERE Email = '{email}'"; // Dangerous!
```

### 🧹 Testing Guidelines

#### Test Naming
```csharp
// ✅ DO: Describe what's being tested
[Fact]
public void PasswordSignIn_WithValidCredentials_ShouldReturnSuccess()

[Theory]
[InlineData("user@test.com", "password123", true)]
[InlineData("invalid", "wrong", false)]
public void ValidateEmail_WithValidEmail_ShouldReturnTrue(string email, string password, bool expected)

// ✅ DO: Name tests by behavior, not implementation
public void EmailValidation_ShouldValidateCorrectly() // Not: EmailValidation_ShouldUseRegex()

// ❌ AVOID: Generic test names
[Fact]
public void Test1() // Bad name
[Fact]
public void LoginTest() // Too generic
```

#### Test Structure
```csharp
// ✅ DO: Arrange-Act-Assert pattern
[Fact]
public void Authentication_WithValidUser_ShouldReturnSuccess()
{
    // Arrange
    var user = CreateTestUser();
    var loginRequest = new LoginRequest { Username = user.UserName, Password = "ValidPassword" };
    
    // Act
    var result = await _authService.LoginAsync(loginRequest);
    
    // Assert
    Assert.True(result.Succeeded);
    Assert.NotNull(result.User);
}

// ✅ DO: Test both positive and negative cases
[Fact]
public void Authentication_WithInvalidPassword_ShouldReturnFailure()
[Fact]
public void Authentication_WithNonExistentUser_ShouldReturnFailure()

// ✅ DO: Use meaningful assertions
Assert.Equal(expectedResult, actualResult);
Assert.True(condition);
Assert.NotNull(object);
Assert.Contains(expectedSubstring, actualString);

// ❌ AVOID: Asserting true without context
Assert.True(result); // What should be true?
```

### 🎨 Razor/Blazor Guidelines

#### Component Structure
```razor
@* @using iPath.Domain.Authentication *@

@if (isLoading)
{
    <MudProgressIndicator />
}
else if (hasError)
{
    <MudAlert Severity="Severity.Error">@errorMessage</MudAlert>
}
else
{
    <EditForm Model="@model" OnValidSubmit="HandleSubmit">
        <!-- Form fields -->
    </EditForm>
}

@code {
    [Parameter] public bool IsLoading { get; set; }
    [Parameter] public string? ErrorMessage { get; set; }
    
    private async Task HandleSubmit()
    {
        // Handle form submission
    }
}
```

#### Performance Optimization
```razor
@* ✅ DO: Use @bind:after for better performance *@
<MudTextField @bind-Value="searchTerm" @bind:after="OnSearchChanged" />

@* ✅ DO: Virtualize long lists *@
<Virtualize Items="@largeList" Context="item">
    <div>@item.Name</div>
</Virtualize>

@* ❌ AVOID: Inefficient data binding *@
<MudTextField Value="@searchTerm" ValueChanged="OnSearchChanged" /> // Less efficient than @bind:after
```

## 🛠️ Development Environment

### Environment Setup Commands
```bash
# Ensure database is up to date
dotnet ef database update

# Create migration (if needed)
dotnet ef migrations add MigrationName

# Run specific migration
dotnet ef database update MigrationName

# Check pending migrations
dotnet ef migrations list
```

### Debugging Tips
```csharp
// ✅ DO: Use conditional compilation for debugging
#if DEBUG
    Console.WriteLine($"Debug: Processing user {userId}");
#endif

// ✅ DO: Use debug guards
System.Diagnostics.Debug.Assert(user != null, "User should not be null");

// ✅ DO: Log detailed information in development
if (_env.IsDevelopment())
{
    _logger.LogDebug("Detailed debug information: {@debugInfo}", debugInfo);
}
```

## 🔄 Repository Guidelines

### Commit Standards
```bash
# Build and test before committing
dotnet build && dotnet test

# Follow conventional commit messages
git commit -m "feat: Add user authentication with migration support"
git commit -m "fix: Resolve login failure on legacy accounts"
git commit -m "docs: Update authentication documentation"
```

### Branch Strategy
```bash
# Feature branches from main
git checkout -b feature/authentication-refactor
git checkout main
git pull origin main
git checkout -b feature/new-feature

# Keep branches up to date
git checkout main
git pull origin main
git checkout feature/authentication-refactor
git rebase main
```

---

## 📋 Special Instructions for this Repository

### Copilot Guidelines
From `.github/copilot-instructions.md`:
- Prefer using FHIR R4 for questionnaire to text conversion

### Authentication-Specific Considerations
- Always use `IPathSignInManager` instead of `SignInManager<User>` for new code
- Handle legacy password migration securely
- Log authentication attempts for security auditing
- Use `IPathSignInResult` for consistent error handling

### Project-Specific Notes
- Solution uses .NET 10.0
- Primary database: EF Core with multiple providers
- Frontend: Blazor Server with MudBlazor components
- Testing: xUnit with FluentAssertions

---

This file should be updated as new patterns emerge and existing guidelines need refinement.