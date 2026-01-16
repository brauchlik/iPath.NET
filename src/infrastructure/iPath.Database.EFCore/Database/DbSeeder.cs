using iPath.Domain.Config;
using iPath.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Linq.Dynamic.Core;

namespace iPath.EF.Core.Database;

public class DbSeeder(iPathDbContext db, 
    IOptions<iPathConfig> opts, 
    UserManager<User> userManager,
    RoleManager<Role> roleManager,
    ILogger<DbSeeder> logger)
{
    public async Task UpdateDatabase()
    {
        if (opts.Value.DbAutoMigrate)
        {
            try
            {
                logger.LogInformation("starting database migrations ...");
                await db.Database.MigrateAsync();
                logger.LogInformation("database migrations done");
            }
            catch (Exception ex)
            {
                logger.LogError("Error during DB Migration", ex);
                var cs = db.Database.GetConnectionString();
                logger.LogInformation("ConnectionString: {0}", cs);
                throw new Exception("No connection to database", ex);
            }
        }
        if (opts.Value.DbSeedingActive)
        {
            await SeedData();
        }
    }


    public async Task SeedData()
    {
        if (db.Roles.Any())
        {
            return;
        }
        else
        {
            await roleManager.CreateAsync(new Role { Name = "Admin" });
            await roleManager.CreateAsync(new Role { Name = "Moderator" });
            await roleManager.CreateAsync(new Role { Name = "Translator" });
            await roleManager.CreateAsync(new Role { Name = "Developer" });
        }

        User? admin = null;
        if (!db.Users.Any())
        {
            admin = new User { Id = Guid.CreateVersion7(), UserName = "Admin", Email = "admin@test.com", IsActive = true, EmailConfirmed = true };
            var pwgen = new RandomPasswordGenerator();
            InitialAdminPassword = pwgen.GenerateRandomPassword();
            var res = await userManager.CreateAsync(admin, InitialAdminPassword);
            if (res.Succeeded)
            {
                await userManager.AddToRoleAsync(admin, "Admin");
                logger.LogInformation("Initial password for user {UserName}: {InitialAdminPassword}", admin.UserName, InitialAdminPassword);
            }
            else
            {
                logger.LogError("Could not create Admin user: {0}", res.Errors.FirstOrDefault().Description);
            }
        }
        else
        {
            admin = await db.Users.OrderBy(x => x.CreatedOn).FirstOrDefaultAsync();
        }


        Community? community = await db.Communities.FirstOrDefaultAsync();
        if (community is null)
        {
            community = Community.Create(Name: "Test Community", Owner: admin);
            await db.Communities.AddAsync(community);

            // add member
            admin.AddToCommunity(community, eMemberRole.User);
        }

        if (!db.Groups.Any() && admin != null)
        {
            var group = Group.Create(Name: "Test Group", Owner: admin, Community: community);
            await db.Groups.AddAsync(group);

            // add member
            admin.AddToGroup(group, eMemberRole.User);
        }


        await db.SaveChangesAsync();
    }

    public static string? InitialAdminPassword = null;
}




// Source - https://stackoverflow.com/a
// Posted by Darkseal, modified by community. See post 'Timeline' for change history
// Retrieved 2025-12-04, License - CC BY-SA 4.0

/// <summary>
/// Generates a Random Password
/// respecting the given strength requirements.
/// </summary>
/// <param name="opts">A valid PasswordOptions object
/// containing the password strength requirements.</param>
/// <returns>A random password</returns>

public class RandomPasswordGenerator 
{
    public string GenerateRandomPassword(PasswordOptions opts = null)
    {
        if (opts == null) opts = new PasswordOptions()
        {
            RequiredLength = 8,
            RequiredUniqueChars = 4,
            RequireDigit = true,
            RequireLowercase = true,
            RequireNonAlphanumeric = true,
            RequireUppercase = true
        };

        string[] randomChars = new[] {
            "ABCDEFGHJKLMNOPQRSTUVWXYZ",    // uppercase 
            "abcdefghijkmnopqrstuvwxyz",    // lowercase
            "0123456789",                   // digits
            "!@$?_-"                        // non-alphanumeric
        };

        Random rand = new Random(Environment.TickCount);
        List<char> chars = new List<char>();

        if (opts.RequireUppercase)
            chars.Insert(rand.Next(0, chars.Count),
                randomChars[0][rand.Next(0, randomChars[0].Length)]);

        if (opts.RequireLowercase)
            chars.Insert(rand.Next(0, chars.Count),
                randomChars[1][rand.Next(0, randomChars[1].Length)]);

        if (opts.RequireDigit)
            chars.Insert(rand.Next(0, chars.Count),
                randomChars[2][rand.Next(0, randomChars[2].Length)]);

        if (opts.RequireNonAlphanumeric)
            chars.Insert(rand.Next(0, chars.Count),
                randomChars[3][rand.Next(0, randomChars[3].Length)]);

        for (int i = chars.Count; i < opts.RequiredLength
            || chars.Distinct().Count() < opts.RequiredUniqueChars; i++)
        {
            string rcs = randomChars[rand.Next(0, randomChars.Length)];
            chars.Insert(rand.Next(0, chars.Count),
                rcs[rand.Next(0, rcs.Length)]);
        }

        return new string(chars.ToArray());
    }
}
