using iPath.Domain.Config;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace iPath.EF.Core.Database;

public class DbSeeder(iPathDbContext db, 
    IOptions<iPathConfig> opts, 
    UserManager<User> userManager,
    RoleManager<Role> roleManager,
    ILogger<DbSeeder> logger)
{
    public void UpdateDatabase()
    {
        if (opts.Value.DbAutoMigrate)
        {
            try
            {
                logger.LogInformation("starting database migrations ...");
                db.Database.Migrate();
                logger.LogInformation("database migrations done");
            }
            catch (Exception ex)
            {
                logger.LogError("Error during DB Migration", ex);
                throw new Exception("No connection to database", ex);
            }
        }
    }
}
