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

        // Register DbContext with SQL token interceptor
        // Note: HttpContextAccessor is registered in Program.cs
        services.AddDbContext<ApplicationDbContext>((serviceProvider, options) =>
        {
            var connectionString = configuration["DOTNET_CONNECTION_STRING"]
                ?? configuration.GetConnectionString("DefaultConnection");

            if (!string.IsNullOrEmpty(connectionString))
            {
                // Remove Authentication parameter - we'll use AccessToken from user's MSAL token instead
                connectionString = RemoveAuthenticationParameter(connectionString);

                // Add SQL token interceptor to set AccessToken from AsyncLocal context
                var logger = serviceProvider.GetRequiredService<ILogger<SqlTokenInterceptor>>();
                var interceptor = new SqlTokenInterceptor(logger);

                options.UseSqlServer(connectionString)
                    .AddInterceptors(interceptor);
            }
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
