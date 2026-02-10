using iPath.API.Authentication;
using iPath.Domain.Authentication;
using iPath.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace iPath.API.Tests.Authentication;

/// <summary>
/// Integration tests for the complete authentication and migration flow
/// </summary>
public class AuthenticationFlowTests
{
    [Fact]
    public void IPathSignInManager_ShouldHandleAllAuthenticationStates()
    {
        // This test ensures our authentication service can handle all required states
        var user = new User { UserName = "testuser" };
        var successResult = IPathSignInResult.Success(user);
        var failedResult = IPathSignInResult.Failed("test");
        var migrationResult = IPathSignInResult.LegacyMigration(user, "token", "email");
        var lockedResult = IPathSignInResult.LockedOut();
        var twoFactorResult = IPathSignInResult.RequiresTwoFactorAuth();
        var notAllowedResult = IPathSignInResult.NotAllowed();

        // Verify all results are created successfully
        Assert.True(successResult.Succeeded);
        Assert.False(failedResult.Succeeded);
        Assert.True(migrationResult.RequiresLegacyMigration);
        Assert.True(lockedResult.IsLockedOut);
        Assert.True(twoFactorResult.RequiresTwoFactor);
        Assert.True(notAllowedResult.IsNotAllowed);
    }

    [Fact]
    public void MD5Hash_ShouldBeConsistent()
    {
        // Test MD5 hash consistency
        var input = "testpassword";
        var hash1 = IPathSignInManager.CreateMD5Hash(input);
        var hash2 = IPathSignInManager.CreateMD5Hash(input);

        Assert.Equal(hash1, hash2);
        // Verify it produces a consistent 32-character hexadecimal string
        Assert.Equal(32, hash1.Length);
        Assert.True(hash1.All(c => char.IsLetterOrDigit(c)));
        
        // Test with known input "test" should produce known hash
        var testHash = IPathSignInManager.CreateMD5Hash("test");
        Assert.Equal("098F6BCD4621D373CADE4E832627B4F6", testHash);
    }

    [Theory]
    [InlineData("user@example.com", true)]  // Valid email
    [InlineData("test.user@domain.co.uk", true)]  // Valid email with subdomain
    [InlineData("invalid-email", false)]  // Invalid - no @
    [InlineData("@domain.com", false)]  // Invalid - no local part
    [InlineData("", false)]  // Empty string
    public void EmailValidation_ShouldWorkCorrectly(string email, bool expectedValid)
    {
        var attr = new System.ComponentModel.DataAnnotations.EmailAddressAttribute();
        var isValid = attr.IsValid(email);
        Assert.Equal(expectedValid, isValid);
    }

    [Fact]
    public void LegacyMigrationResult_ShouldContainRequiredData()
    {
        // Test that legacy migration results contain all necessary data
        var user = new User { UserName = "testuser", Email = "test@example.com" };
        var result = IPathSignInResult.LegacyMigration(user, "migration-token", "base64-email");

        Assert.True(result.RequiresLegacyMigration);
        Assert.Equal(user, result.User);
        Assert.Equal("migration-token", result.MigrationToken);
        Assert.Equal("base64-email", result.MigrationEmail);
        Assert.False(result.Succeeded);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void AllResultFactoryMethods_ShouldCreateValidResults()
    {
        // Test all factory methods create valid results
        var user = new User { UserName = "testuser" };

        var success = IPathSignInResult.Success(user);
        Assert.True(success.Succeeded);
        Assert.Equal(user, success.User);

        var failed = IPathSignInResult.Failed("Error message");
        Assert.False(failed.Succeeded);
        Assert.Equal("Error message", failed.ErrorMessage);

        var locked = IPathSignInResult.LockedOut();
        Assert.True(locked.IsLockedOut);

        var twoFactor = IPathSignInResult.RequiresTwoFactorAuth();
        Assert.True(twoFactor.RequiresTwoFactor);

        var notAllowed = IPathSignInResult.NotAllowed();
        Assert.True(notAllowed.IsNotAllowed);
    }
}