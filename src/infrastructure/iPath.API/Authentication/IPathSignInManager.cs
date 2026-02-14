using iPath.Domain.Authentication;
using iPath.EF.Core.Database;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;

namespace iPath.API.Authentication;

/// <summary>
/// Custom SignInManager that provides enhanced authentication capabilities for iPath,
/// including email/username resolution and legacy password migration support.
/// </summary>
public class IPathSignInManager : SignInManager<User>
{
    private readonly iPathDbContext _dbContext;
    private readonly ILogger<IPathSignInManager> _logger;
    private readonly IEmailSender<User> _emailSender;

    public IPathSignInManager(
        UserManager<User> userManager,
        IHttpContextAccessor contextAccessor,
        IUserClaimsPrincipalFactory<User> claimsFactory,
        IOptions<IdentityOptions> optionsAccessor,
        ILogger<IPathSignInManager> logger,
        IAuthenticationSchemeProvider schemeProvider,
        IUserConfirmation<User> confirmation,
        iPathDbContext dbContext,
        IEmailSender<User> emailSender)
        : base(userManager, contextAccessor, claimsFactory, optionsAccessor, logger, schemeProvider, confirmation)
    {
        _dbContext = dbContext;
        _logger = logger;
        _emailSender = emailSender;
    }

    /// <summary>
    /// Attempts to sign in a user using either email or username, with legacy password migration support.
    /// </summary>
    /// <param name="usernameOrEmail">The username or email address of the user.</param>
    /// <param name="password">The password to verify.</param>
    /// <param name="isPersistent">Whether the sign-in cookie should be persistent.</param>
    /// <param name="lockoutOnFailure">Whether to lock out the user on sign-in failure.</param>
    /// <returns>A task that represents the asynchronous operation, containing the sign-in result.</returns>
    public async Task<IPathSignInResult> PasswordSignInWithEmailOrUsernameAsync(
        string usernameOrEmail, 
        string password, 
        bool isPersistent, 
        bool lockoutOnFailure = false)
    {
        try
        {
            // Resolve username if email was provided
            var resolvedUsername = await ResolveUsernameOrEmailAsync(usernameOrEmail);
            
            // Attempt standard Identity sign-in first
            var standardResult = await base.PasswordSignInAsync(resolvedUsername, password, isPersistent, lockoutOnFailure);
            
            if (standardResult.Succeeded)
            {
                _logger.LogInformation("User {user} logged in.", resolvedUsername);
                var user = await UserManager.FindByNameAsync(resolvedUsername);
                return IPathSignInResult.Success(user!);
            }

            if (standardResult.IsLockedOut)
            {
                _logger.LogWarning("User account locked out for {UsernameOrEmail}", usernameOrEmail);
                return IPathSignInResult.LockedOut();
            }

            if (standardResult.IsNotAllowed)
            {
                _logger.LogWarning("User account not allowed for {UsernameOrEmail}", usernameOrEmail);
                return IPathSignInResult.NotAllowed();
            }

            if (standardResult.RequiresTwoFactor)
            {
                return IPathSignInResult.RequiresTwoFactorAuth();
            }

            // Check for legacy password migration
            var legacyResult = await TryLegacyPasswordMigrationAsync(usernameOrEmail, password);
            if (legacyResult.RequiresLegacyMigration)
            {
                return legacyResult;
            }

            return IPathSignInResult.Failed("Error: Invalid login attempt.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during sign-in for {UsernameOrEmail}", usernameOrEmail);
            return IPathSignInResult.Failed("An error occurred during sign-in. Please try again.");
        }
    }

    /// <summary>
    /// Resolves a username or email to a valid username for authentication.
    /// </summary>
    /// <param name="input">The username or email address to resolve.</param>
    /// <returns>A task that represents the asynchronous operation, containing the resolved username.</returns>
    private async Task<string> ResolveUsernameOrEmailAsync(string input)
    {
        var emailAttr = new EmailAddressAttribute();
        if (emailAttr.IsValid(input))
        {
            try
            {
                var user = await UserManager.FindByEmailAsync(input);
                if (user != null)
                {
                    return user.UserName ?? input; // Fallback to original input
                }
                
                // Email not found - return original input for security (don't reveal email doesn't exist)
                _logger.LogInformation("Email not found during username resolution: {Email}", input);
                return input;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resolving email to username: {Email}", input);
                return input; // Fallback to original input
            }
        }
        
        return input; // Already a username
    }

    /// <summary>
    /// Attempts legacy password verification and initiates migration if successful.
    /// </summary>
    /// <param name="usernameOrEmail">The username or email address.</param>
    /// <param name="password">The password to verify.</param>
    /// <returns>A task that represents the asynchronous operation, containing the migration result.</returns>
    private async Task<IPathSignInResult> TryLegacyPasswordMigrationAsync(string usernameOrEmail, string password)
    {
        try
        {
            // Find user by username or email (including legacy migration check)
            var user = await _dbContext.Users
                .FirstOrDefaultAsync(u => u.UserName == usernameOrEmail || u.Email == usernameOrEmail);

            if (user == null || string.IsNullOrWhiteSpace(user.ipath2_password))
            {
                return IPathSignInResult.Failed("Error: Invalid login attempt.");
            }

            // Verify legacy MD5 password
            if (!VerifyLegacyPassword(user, password))
            {
                return IPathSignInResult.Failed("Error: Invalid login attempt.");
            }

            // Legacy password verified - initiate migration flow
            _logger.LogInformation("Legacy password verified for user {UserId}, initiating migration", user.Id);
            return await InitiateLegacyMigrationAsync(user);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during legacy password migration check for {UsernameOrEmail}", usernameOrEmail);
            return IPathSignInResult.Failed("An error occurred during authentication.");
        }
    }

    /// <summary>
    /// Verifies a password against the legacy MD5 hash stored in the user record.
    /// </summary>
    /// <param name="user">The user with the legacy password.</param>
    /// <param name="password">The password to verify.</param>
    /// <returns>True if the password matches the legacy hash, false otherwise.</returns>
    private bool VerifyLegacyPassword(User user, string password)
    {
        if (string.IsNullOrWhiteSpace(user.ipath2_password))
            return false;

        var legacyHash = CreateMD5Hash(password).ToLowerInvariant();
        return legacyHash == user.ipath2_password.ToLowerInvariant();
    }

    /// <summary>
    /// Initiates the legacy password migration process by generating a migration token.
    /// </summary>
    /// <param name="user">The user to migrate.</param>
    /// <returns>A task that represents the asynchronous operation, containing the migration result.</returns>
    private async Task<IPathSignInResult> InitiateLegacyMigrationAsync(User user)
    {
        if (string.IsNullOrWhiteSpace(user.Email))
        {
            _logger.LogError("User {UserId} requires legacy migration but has no email address", user.Id);
            return IPathSignInResult.Failed("Email address required for password migration. Please contact support.");
        }

        try
        {
            // Generate password reset token for migration
            var token = await UserManager.GeneratePasswordResetTokenAsync(user);
            var emailB64 = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(user.Email));

            _logger.LogInformation("Legacy migration initiated for user {UserId} with email {Email}", user.Id, user.Email);

            return IPathSignInResult.LegacyMigration(user, token, emailB64);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initiating legacy migration for user {UserId}", user.Id);
            return IPathSignInResult.Failed("Failed to initiate password migration. Please contact support.");
        }
    }

    /// <summary>
    /// Creates an MD5 hash of the input string for legacy password verification.
    /// </summary>
    /// <param name="input">The string to hash.</param>
    /// <returns>The MD5 hash as a hexadecimal string.</returns>
    public static string CreateMD5Hash(string input)
    {
        // Use input string to calculate MD5 hash
        using (MD5 md5 = MD5.Create())
        {
            byte[] inputBytes = Encoding.ASCII.GetBytes(input);
            byte[] hashBytes = md5.ComputeHash(inputBytes);

            // Convert the byte array to hexadecimal string
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < hashBytes.Length; i++)
            {
                sb.Append(hashBytes[i].ToString("X2"));
            }
            return sb.ToString();
        }
    }
}