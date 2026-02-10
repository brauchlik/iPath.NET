using iPath.API.Authentication;
using iPath.Domain.Authentication;
using iPath.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace iPath.API.Tests.Authentication;

/// <summary>
/// Basic unit tests for IPathSignInManager functionality.
/// These tests focus on static methods and result creation without complex mocking.
/// </summary>
public class IPathSignInManagerTests
{
    [Fact]
    public void CreateMD5Hash_WithKnownInput_ShouldReturnExpectedHash()
    {
        // Arrange
        var input = "test";
        var expectedHash = "098F6BCD4621D373CADE4E832627B4F6"; // MD5 of "test"

        // Act
        var result = IPathSignInManager.CreateMD5Hash(input);

        // Assert
        Assert.Equal(expectedHash, result);
    }

    [Fact]
    public void CreateMD5Hash_WithEmptyString_ShouldReturnExpectedHash()
    {
        // Arrange
        var input = "";
        var expectedHash = "D41D8CD98F00B204E9800998ECF8427E"; // MD5 of empty string

        // Act
        var result = IPathSignInManager.CreateMD5Hash(input);

        // Assert
        Assert.Equal(expectedHash, result);
    }

    [Fact]
    public void IPathSignInResult_Success_ShouldCreateCorrectResult()
    {
        // Arrange
        var user = new User { UserName = "testuser" };

        // Act
        var result = IPathSignInResult.Success(user);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal(user, result.User);
        Assert.False(result.IsLockedOut);
        Assert.False(result.RequiresLegacyMigration);
        Assert.False(result.RequiresTwoFactor);
        Assert.Null(result.ErrorMessage);
        Assert.Null(result.MigrationToken);
        Assert.Null(result.MigrationEmail);
    }

    [Fact]
    public void IPathSignInResult_Failed_ShouldCreateCorrectResult()
    {
        // Arrange
        var errorMessage = "Invalid credentials";

        // Act
        var result = IPathSignInResult.Failed(errorMessage);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal(errorMessage, result.ErrorMessage);
        Assert.False(result.IsLockedOut);
        Assert.False(result.RequiresLegacyMigration);
        Assert.False(result.RequiresTwoFactor);
        Assert.Null(result.User);
        Assert.Null(result.MigrationToken);
        Assert.Null(result.MigrationEmail);
    }

    [Fact]
    public void IPathSignInResult_LegacyMigration_ShouldCreateCorrectResult()
    {
        // Arrange
        var user = new User { UserName = "testuser" };
        var token = "migration-token";
        var email = "dGVzdEBleGFtcGxlLmNvbQ=="; // base64 encoded email

        // Act
        var result = IPathSignInResult.LegacyMigration(user, token, email);

        // Assert
        Assert.False(result.Succeeded);
        Assert.True(result.RequiresLegacyMigration);
        Assert.Equal(user, result.User);
        Assert.Equal(token, result.MigrationToken);
        Assert.Equal(email, result.MigrationEmail);
        Assert.False(result.IsLockedOut);
        Assert.False(result.RequiresTwoFactor);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void IPathSignInResult_LockedOut_ShouldCreateCorrectResult()
    {
        // Act
        var result = IPathSignInResult.LockedOut();

        // Assert
        Assert.False(result.Succeeded);
        Assert.True(result.IsLockedOut);
        Assert.False(result.RequiresLegacyMigration);
        Assert.False(result.RequiresTwoFactor);
        Assert.Null(result.User);
        Assert.Null(result.ErrorMessage);
        Assert.Null(result.MigrationToken);
        Assert.Null(result.MigrationEmail);
    }

    [Fact]
    public void IPathSignInResult_RequiresTwoFactorAuth_ShouldCreateCorrectResult()
    {
        // Act
        var result = IPathSignInResult.RequiresTwoFactorAuth();

        // Assert
        Assert.False(result.Succeeded);
        Assert.False(result.IsLockedOut);
        Assert.False(result.RequiresLegacyMigration);
        Assert.True(result.RequiresTwoFactor);
        Assert.Null(result.User);
        Assert.Null(result.ErrorMessage);
        Assert.Null(result.MigrationToken);
        Assert.Null(result.MigrationEmail);
    }

    [Fact]
    public void IPathSignInResult_NotAllowed_ShouldCreateCorrectResult()
    {
        // Act
        var result = IPathSignInResult.NotAllowed();

        // Assert
        Assert.False(result.Succeeded);
        Assert.False(result.IsLockedOut);
        Assert.False(result.RequiresLegacyMigration);
        Assert.False(result.RequiresTwoFactor);
        Assert.True(result.IsNotAllowed);
        Assert.Null(result.User);
        Assert.Null(result.ErrorMessage);
        Assert.Null(result.MigrationToken);
        Assert.Null(result.MigrationEmail);
    }

    [Theory]
    [InlineData("hello@example.com", true)]
    [InlineData("user@domain.co.uk", true)]
    [InlineData("test.email+tag@example.com", true)]
    [InlineData("plainusername", false)]
    [InlineData("invalid-email@", false)]
    [InlineData("@domain.com", false)]
    [InlineData("", false)]
    public void IsValidEmail_ShouldValidateCorrectly(string email, bool expectedValid)
    {
        // Act
        var emailAttr = new System.ComponentModel.DataAnnotations.EmailAddressAttribute();
        var isValid = emailAttr.IsValid(email);

        // Assert
        Assert.Equal(expectedValid, isValid);
    }

    [Fact]
    public void IsValidEmail_WithNull_ReturnsTrue()
    {
        // Act
        var emailAttr = new System.ComponentModel.DataAnnotations.EmailAddressAttribute();
        var isValid = emailAttr.IsValid(null);

        // Assert
        // Note: EmailAddressAttribute considers null as valid (optional field)
        Assert.True(isValid);
    }
}