using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BugBoard.Api.Data;

/// <summary>
/// Used ONLY by the "dotnet ef" command line tool. It lets "dotnet ef migrations add"
/// work without starting the whole web app (which would need the JWT secret key).
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<BugBoardDbContext>
{
    public BugBoardDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<BugBoardDbContext>()
            .UseSqlite("Data Source=bugboard.db")
            .Options;
        return new BugBoardDbContext(options);
    }
}
