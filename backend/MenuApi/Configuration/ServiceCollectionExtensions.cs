using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MenuApi.Data;
using MenuApi.Services;
using OpenFga.Sdk.Client;
using OpenFga.Sdk.Configuration;

namespace MenuApi.Configuration;

/// <summary>
/// Extension methods for registering application services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers application services and configuration
    /// </summary>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Configure options
        services.AddOptions<DatabaseOptions>()
            .Configure(options => options.ConnectionString =
                configuration["DOTNET_CONNECTION_STRING"] ??
                configuration.GetConnectionString("DefaultConnection") ??
                string.Empty)
            .ValidateOnStart();

        services.AddOptions<OpenFgaOptions>()
            .Configure(options =>
            {
                options.ApiUrl = configuration["OPENFGA_API_URL"] ?? "http://localhost:8080";
                options.StoreId = configuration["OPENFGA_STORE_ID"] ?? string.Empty;
            })
            .ValidateOnStart();

        // Register DbContext
        services.AddDbContext<ApplicationDbContext>((serviceProvider, options) =>
        {
            var connectionString = configuration["DOTNET_CONNECTION_STRING"]
                ?? configuration.GetConnectionString("DefaultConnection");

            if (!string.IsNullOrEmpty(connectionString))
            {
                // Transform connection string to use Managed Identity if it doesn't contain a password
                // Supports both legacy password-based and modern managed identity authentication
                connectionString = TransformConnectionStringForManagedIdentity(connectionString);
                options.UseSqlServer(connectionString);
            }
        });

        // Register OpenFGA client
        services.AddSingleton<OpenFgaClient>(sp =>
        {
            var apiUrl = configuration["OPENFGA_API_URL"] ?? "http://localhost:8080";
            var storeId = configuration["OPENFGA_STORE_ID"] ?? string.Empty;

            var clientConfiguration = new ClientConfiguration
            {
                ApiUrl = apiUrl,
                StoreId = storeId
            };

            return new OpenFgaClient(clientConfiguration);
        });

        // Register application services
        services.AddScoped<IMenuService, MenuService>();
        services.AddScoped<IPowerBIService, PowerBIService>();
        services.AddScoped<IAuthorizationService, AuthorizationService>();
        services.AddScoped<IClaimsPrincipalParser, ClaimsPrincipalParser>();
        services.AddScoped<IJwtTokenValidator, JwtTokenValidator>();

        return services;
    }

    /// <summary>
    /// Transforms a SQL Server connection string to use Managed Identity authentication
    /// if it doesn't already contain password credentials.
    /// </summary>
    /// <remarks>
    /// For Azure SQL Database:
    /// - If the connection string contains "Password=" or "pwd=", it's left unchanged (legacy mode)
    /// - Otherwise, adds "Authentication=Active Directory Default" for managed identity auth
    ///
    /// Active Directory Default tries authentication in this order:
    /// 1. Environment variables (for local dev with service principal)
    /// 2. Managed Identity (for Azure-hosted apps)
    /// 3. Visual Studio / Azure CLI (for local development)
    /// </remarks>
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
