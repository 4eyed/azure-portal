using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MenuApi.Data;
using MenuApi.Services;
using OpenFga.Sdk.Client;
using OpenFga.Sdk.Configuration;
using Azure.Core;
using Azure.Identity;
using MenuApi.Infrastructure;

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
        // Note: ValidateOnStart() removed to avoid blocking during Azure Functions cold start
        services.AddOptions<DatabaseOptions>()
            .Configure(options => options.ConnectionString =
                configuration["DOTNET_CONNECTION_STRING"] ??
                configuration.GetConnectionString("DefaultConnection") ??
                string.Empty);

        services.AddOptions<OpenFgaOptions>()
            .Configure(options =>
            {
                options.ApiUrl = configuration["OPENFGA_API_URL"] ?? "http://localhost:8080";
                options.StoreId = configuration["OPENFGA_STORE_ID"] ?? string.Empty;
            });

        services.AddScoped<SqlTokenInterceptor>();

        services.AddDbContext<ApplicationDbContext>((serviceProvider, options) =>
        {
            var logger = serviceProvider.GetRequiredService<ILogger<ApplicationDbContext>>();
            var interceptor = serviceProvider.GetRequiredService<SqlTokenInterceptor>();

            var connectionString = configuration["DOTNET_CONNECTION_STRING"]
                ?? configuration.GetConnectionString("DefaultConnection");

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException("A SQL connection string was not provided. Set DOTNET_CONNECTION_STRING or ConnectionStrings:DefaultConnection in app settings.");
            }

            // Check if running in Azure
            var isAzure = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME"));

            var sanitized = string.Join(";", connectionString
                .Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Where(part => !part.StartsWith("Password", StringComparison.OrdinalIgnoreCase) &&
                               !part.StartsWith("Pwd", StringComparison.OrdinalIgnoreCase)));

            logger.LogInformation("Configuring SQL Server DbContext (Environment: {Environment}, ConnectionString: {ConnectionString})",
                isAzure ? "Azure" : "Local Dev", sanitized);

            options.UseSqlServer(connectionString);

            // Only add SQL token interceptor in LOCAL DEV mode
            // In Azure, Managed Identity is used via connection string (no interceptor needed)
            if (!isAzure)
            {
                logger.LogInformation("Registering SqlTokenInterceptor for local dev (user SQL tokens)");
                options.AddInterceptors(interceptor);
            }
            else
            {
                logger.LogInformation("Using Managed Identity authentication (no interceptor)");
            }
        }, ServiceLifetime.Scoped);

        // Expose DefaultAzureCredential so managed identity can be reused (Power BI, etc.)
        services.AddSingleton<TokenCredential>(_ => new DefaultAzureCredential());

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

        return services;
    }
}
