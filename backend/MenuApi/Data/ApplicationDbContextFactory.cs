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

        // Transform connection string to use Managed Identity if it doesn't contain a password
        connectionString = TransformConnectionStringForManagedIdentity(connectionString);

        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseSqlServer(connectionString);

        return new ApplicationDbContext(optionsBuilder.Options);
    }

    /// <summary>
    /// Transforms a SQL Server connection string to use Managed Identity authentication
    /// if it doesn't already contain password credentials.
    /// </summary>
    private static string TransformConnectionStringForManagedIdentity(string connectionString)
    {
        // If connection string already has password, don't modify it (backwards compatibility)
        if (connectionString.Contains("Password=", StringComparison.OrdinalIgnoreCase) ||
            connectionString.Contains("pwd=", StringComparison.OrdinalIgnoreCase))
        {
            return connectionString;
        }

        // If already has Authentication parameter, don't modify
        if (connectionString.Contains("Authentication=", StringComparison.OrdinalIgnoreCase))
        {
            return connectionString;
        }

        // Add managed identity authentication
        var separator = connectionString.Contains(';') && !connectionString.TrimEnd().EndsWith(';')
            ? ";"
            : string.Empty;

        return $"{connectionString}{separator};Authentication=Active Directory Default";
    }
}
