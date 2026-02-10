using Microsoft.AspNetCore.Identity;

namespace iPath.Domain.Authentication;

/// <summary>
/// Represents the result of an iPath authentication operation, including legacy migration states.
/// </summary>
public class IPathSignInResult
{
    /// <summary>
    /// Gets a value indicating whether the sign-in was successful.
    /// </summary>
    public bool Succeeded { get; set; }

    /// <summary>
    /// Gets a value indicating whether the user is locked out.
    /// </summary>
    public bool IsLockedOut { get; set; }

    /// <summary>
    /// Gets a value indicating whether the user is not allowed to sign in.
    /// </summary>
    public bool IsNotAllowed { get; set; }

    /// <summary>
    /// Gets a value indicating whether two-factor authentication is required.
    /// </summary>
    public bool RequiresTwoFactor { get; set; }

    /// <summary>
    /// Gets a value indicating whether legacy password migration is required.
    /// </summary>
    public bool RequiresLegacyMigration { get; set; }

    /// <summary>
    /// Gets the user associated with the sign-in attempt.
    /// </summary>
    public User? User { get; set; }

    /// <summary>
    /// Gets the error message if the sign-in failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets the migration token for legacy password migration.
    /// </summary>
    public string? MigrationToken { get; set; }

    /// <summary>
    /// Gets the base64-encoded email for migration confirmation.
    /// </summary>
    public string? MigrationEmail { get; set; }

    /// <summary>
    /// Creates a successful sign-in result.
    /// </summary>
    /// <param name="user">The user that signed in.</param>
    /// <returns>A successful sign-in result.</returns>
    public static IPathSignInResult Success(User user)
    {
        return new IPathSignInResult
        {
            Succeeded = true,
            User = user
        };
    }

    /// <summary>
    /// Creates a failed sign-in result.
    /// </summary>
    /// <param name="errorMessage">The error message describing the failure.</param>
    /// <returns>A failed sign-in result.</returns>
    public static IPathSignInResult Failed(string errorMessage)
    {
        return new IPathSignInResult
        {
            Succeeded = false,
            ErrorMessage = errorMessage
        };
    }

    /// <summary>
    /// Creates a result indicating legacy password migration is required.
    /// </summary>
    /// <param name="user">The user requiring migration.</param>
    /// <param name="migrationToken">The migration token.</param>
    /// <param name="migrationEmail">The base64-encoded email for migration.</param>
    /// <returns>A result indicating legacy migration is required.</returns>
    public static IPathSignInResult LegacyMigration(User user, string migrationToken, string migrationEmail)
    {
        return new IPathSignInResult
        {
            RequiresLegacyMigration = true,
            User = user,
            MigrationToken = migrationToken,
            MigrationEmail = migrationEmail
        };
    }

    /// <summary>
    /// Creates a result indicating the user is locked out.
    /// </summary>
    /// <returns>A locked out result.</returns>
    public static IPathSignInResult LockedOut()
    {
        return new IPathSignInResult
        {
            IsLockedOut = true
        };
    }

    /// <summary>
    /// Creates a result indicating two-factor authentication is required.
    /// </summary>
    /// <returns>A result requiring two-factor authentication.</returns>
    public static IPathSignInResult RequiresTwoFactorAuth()
    {
        return new IPathSignInResult
        {
            RequiresTwoFactor = true
        };
    }

    /// <summary>
    /// Creates a result indicating the user is not allowed to sign in.
    /// </summary>
    /// <returns>A result indicating the user is not allowed.</returns>
    public static IPathSignInResult NotAllowed()
    {
        return new IPathSignInResult
        {
            IsNotAllowed = true
        };
    }
}