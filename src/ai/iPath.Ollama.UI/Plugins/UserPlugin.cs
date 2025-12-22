using iPath.Domain.Entities;
using iPath.EF.Core.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace iPath.Ollama.UI.Plugins;


public class UserPlugin(iPathDbContext db)
{
    [KernelFunction("query_users")]
    [Description("Gets a queriable of iPath Users")]
    [return: Description("queriable of users")]
    public IQueryable<User> QueryUsers()
        => db.Users.AsNoTracking();


    [KernelFunction("user_count")]
    [Description("Gets count of iPath Users")]
    [return: Description("user count")]
    public async Task<int> GetUserCount()
    {
        return await db.Users.AsNoTracking().CountAsync();
    }
}
