using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MenuApi.Data;

/// <summary>
/// Design-time factory for ApplicationDbContext used by EF Core migrations
/// </summary>
public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        // Get connection string from environment variable
        var connectionString = Environment.GetEnvironmentVariable("DOTNET_CONNECTION_STRING");

        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException(
                "DOTNET_CONNECTION_STRING environment variable not set. " +
                "Set it before running migrations: " +
                "export DOTNET_CONNECTION_STRING=\"your-connection-string\"");
        }

        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseSqlServer(connectionString);

        return new ApplicationDbContext(optionsBuilder.Options);
    }
}
