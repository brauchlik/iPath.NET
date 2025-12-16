using iPath.Domain.Config;
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
                throw new Exception("No connection to database", ex);
            }
        }
        if (opts.Value.DbSeedingAvtice)
        {
            await SeedData();
        }
    }


    public async Task SeedData()
    {
        if (!db.Roles.Any())
        {
            await roleManager.CreateAsync(new Role { Name = "Admin" });
            await roleManager.CreateAsync(new Role { Name = "Moderator" });
            await roleManager.CreateAsync(new Role { Name = "Translator" });
            await roleManager.CreateAsync(new Role { Name = "Developer" });
        }

        User? admin;
        if (!db.Users.Any())
        {
            admin = new User { UserName = "Admin", Email = "admin@test.com", IsActive = true, EmailConfirmed = true };
            var res = await userManager.CreateAsync(admin, "Test+1234");
            if (res.Succeeded)
            {
                await userManager.AddToRoleAsync(admin, "Admin");
            }
        }
        else
        {
            admin = await db.Users.OrderBy(x => x.CreatedOn).FirstOrDefaultAsync();
        }


        if (!db.Communities.Any() && admin != null)
        {
            await db.Communities.AddAsync(new Community { Name = "Test Community", OwnerId = admin.Id });
        }

        if (!db.Groups.Any() && admin != null)
        {
            await db.Groups.AddAsync(new Group { Name = "Test Group", OwnerId = admin.Id });
        }

        await db.SaveChangesAsync();
    }

}
