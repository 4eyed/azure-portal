using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MenuApi.Data;

public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();

        // For migrations, use a placeholder connection string
        // The actual connection string comes from environment variables at runtime
        var connectionString = Environment.GetEnvironmentVariable("DOTNET_CONNECTION_STRING")
            ?? "Server=localhost;Database=MenuApp;Integrated Security=true;TrustServerCertificate=true;";

        optionsBuilder.UseSqlServer(connectionString);

        return new ApplicationDbContext(optionsBuilder.Options);
    }
}
