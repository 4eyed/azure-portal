using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using MenuApi.Data;
using MenuApi.Services;
using MenuApi.Infrastructure;
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

        // Register DbContext with SQL token interceptor
        // Note: HttpContextAccessor is registered in Program.cs
        services.AddDbContext<ApplicationDbContext>((serviceProvider, options) =>
        {
            var logger = serviceProvider.GetRequiredService<ILogger<SqlTokenInterceptor>>();

            // Get raw connection string from configuration
            var rawConnectionString = configuration["DOTNET_CONNECTION_STRING"]
                ?? configuration.GetConnectionString("DefaultConnection");

            logger.LogInformation("🔍 ========================================");
            logger.LogInformation("🔍 DATABASE CONNECTION STRING PROCESSING");
            logger.LogInformation("🔍 ========================================");

            if (string.IsNullOrEmpty(rawConnectionString))
            {
                logger.LogError("❌ No connection string found in configuration!");
                logger.LogError("   Checked: DOTNET_CONNECTION_STRING and ConnectionStrings:DefaultConnection");
                return;
            }

            // Log RAW value from configuration (before any processing)
            var rawSanitized = string.Join(";", rawConnectionString.Split(';')
                .Select(s =>
                {
                    if (s.Contains("Password", StringComparison.OrdinalIgnoreCase) ||
                        s.Contains("Pwd", StringComparison.OrdinalIgnoreCase))
                    {
                        return s.Split('=')[0] + "=***REDACTED***";
                    }
                    return s;
                }));

            logger.LogInformation("🔍 RAW Connection String (from config):");
            logger.LogInformation("   Length: {Length} chars", rawConnectionString.Length);
            logger.LogInformation("   Value: {ConnectionString}", rawSanitized);

            // Check for Authentication parameter BEFORE processing
            var hasAuthBefore = rawConnectionString.Contains("Authentication", StringComparison.OrdinalIgnoreCase);
            logger.LogInformation("   Contains 'Authentication': {HasAuth}", hasAuthBefore ? "YES" : "NO");

            if (hasAuthBefore)
            {
                var authMatch = System.Text.RegularExpressions.Regex.Match(
                    rawConnectionString,
                    @"Authentication\s*=\s*([^;]+)",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase
                );
                if (authMatch.Success)
                {
                    logger.LogInformation("   Authentication value: {AuthValue}", authMatch.Groups[1].Value);
                }
            }

            // Check if running in Azure
            var isAzure = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME"));
            logger.LogInformation("🔍 Environment Detection:");
            logger.LogInformation("   IsAzure: {IsAzure}", isAzure);
            logger.LogInformation("   WEBSITE_SITE_NAME: {SiteName}",
                Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME") ?? "[not set]");

            var connectionString = rawConnectionString;

            // Only remove Authentication parameter in local dev (where we'll use user tokens)
            // In Azure, keep "Authentication=Active Directory Default" for Managed Identity
            if (!isAzure)
            {
                logger.LogInformation("🔍 Local Development Mode:");
                logger.LogInformation("   Removing 'Authentication' parameter (will use user SQL tokens instead)");
                connectionString = RemoveAuthenticationParameter(connectionString);
            }
            else
            {
                logger.LogInformation("🔍 Azure Production Mode:");
                logger.LogInformation("   Keeping 'Authentication' parameter (will use Managed Identity)");
            }

            // Log FINAL connection string (after processing)
            var finalSanitized = string.Join(";", connectionString.Split(';')
                .Where(s => !s.Contains("Password", StringComparison.OrdinalIgnoreCase) &&
                           !s.Contains("Pwd", StringComparison.OrdinalIgnoreCase)));

            var hasAuthAfter = connectionString.Contains("Authentication", StringComparison.OrdinalIgnoreCase);

            logger.LogInformation("🔍 FINAL Connection String (for EF Core):");
            logger.LogInformation("   Length: {Length} chars", connectionString.Length);
            logger.LogInformation("   Value: {ConnectionString}", finalSanitized);
            logger.LogInformation("   Contains 'Authentication': {HasAuth}", hasAuthAfter ? "YES" : "NO");

            if (!isAzure && hasAuthAfter)
            {
                logger.LogWarning("⚠️  WARNING: Authentication parameter still present in local mode!");
                logger.LogWarning("   This should have been removed by RemoveAuthenticationParameter()");
            }

            if (isAzure && !hasAuthAfter)
            {
                logger.LogError("❌ ERROR: Authentication parameter missing in Azure mode!");
                logger.LogError("   Managed Identity requires 'Authentication=Active Directory Default'");
                logger.LogError("   Check GitHub Secret: DOTNET_CONNECTION_STRING");
            }

            logger.LogInformation("🔍 ========================================");

            // Add SQL token interceptor to set AccessToken from AsyncLocal context (local dev)
            // In production, this will just pass through and use Managed Identity from connection string
            var interceptor = new SqlTokenInterceptor(logger);

            options.UseSqlServer(connectionString)
                .AddInterceptors(interceptor);
        }, ServiceLifetime.Scoped); // Must be Scoped (not Singleton) for per-request SQL tokens

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
    /// Removes Authentication parameter from connection string
    /// We use AccessToken instead (set by SqlTokenInterceptor from user's MSAL token)
    /// </summary>
    private static string RemoveAuthenticationParameter(string connectionString)
    {
        // Remove Authentication= parameter and any surrounding semicolons
        return System.Text.RegularExpressions.Regex.Replace(
            connectionString,
            @";?\s*Authentication\s*=\s*[^;]+;?",
            "",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase
        ).TrimEnd(';');
    }
}
