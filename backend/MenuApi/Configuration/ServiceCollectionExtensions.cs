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

        return services;
    }
}
