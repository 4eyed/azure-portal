using System.Text;
using System.Threading;
using Azure.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MenuApi.Data;
using MenuApi.Infrastructure;

namespace MenuApi.Functions;

/// <summary>
/// Provides a verbose diagnostics report for SQL connectivity when running inside Azure.
/// This endpoint focuses on the managed identity scenario and surfaces the exact
/// configuration the container is using at runtime.
/// </summary>
public class SqlDebug
{
    private static readonly string[] ManagedIdentityScopes =
        ["https://database.windows.net//.default"];

    private readonly ILogger<SqlDebug> _logger;
    private readonly IConfiguration _configuration;
    private readonly ApplicationDbContext _dbContext;
    private readonly TokenCredential _credential;

    public SqlDebug(
        ILogger<SqlDebug> logger,
        IConfiguration configuration,
        ApplicationDbContext dbContext,
        TokenCredential credential)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _credential = credential ?? throw new ArgumentNullException(nameof(credential));
    }

    [Function("SqlDebug")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "debug/sql-test")] HttpRequest req)
    {
        var report = new StringBuilder();
        report.AppendLine("========================================");
        report.AppendLine("SQL Connectivity Diagnostics");
        report.AppendLine($"Timestamp: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        report.AppendLine("========================================");
        report.AppendLine();

        AppendEnvironmentSection(report);
        AppendConnectionStringSection(report);

        try
        {
            await AppendEntityFrameworkProbe(report);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "EF Core probe failed");
            report.AppendLine("EF CORE PROBE");
            report.AppendLine("----------------------------------------");
            report.AppendLine($"❌ Failed to execute DbContext query: {ex.Message}");
            report.AppendLine();
        }

        try
        {
            await AppendManagedIdentityProbe(report);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Managed identity probe failed");
            report.AppendLine("MANAGED IDENTITY PROBE");
            report.AppendLine("----------------------------------------");
            report.AppendLine($"❌ Failed to acquire token or open SQL connection: {ex.Message}");
            report.AppendLine();
        }

        report.AppendLine("========================================");
        report.AppendLine("End of Diagnostics Report");
        report.AppendLine("========================================");

        return new CorsObjectResult(new
        {
            success = true,
            timestamp = DateTime.UtcNow,
            report = report.ToString()
        });
    }

    private void AppendEnvironmentSection(StringBuilder report)
    {
        report.AppendLine("ENVIRONMENT");
        report.AppendLine("----------------------------------------");
        report.AppendLine($"WEBSITE_SITE_NAME: {Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME") ?? "[not set]"}");
        report.AppendLine($"WEBSITES_PORT: {Environment.GetEnvironmentVariable("WEBSITES_PORT") ?? "[not set]"}");
        report.AppendLine($"FUNCTIONS_WORKER_RUNTIME: {Environment.GetEnvironmentVariable("FUNCTIONS_WORKER_RUNTIME") ?? "[not set]"}");
        report.AppendLine($"IDENTITY_ENDPOINT: {(string.IsNullOrEmpty(Environment.GetEnvironmentVariable("IDENTITY_ENDPOINT")) ? "[not set]" : "[set]")}");
        report.AppendLine($"MSI_ENDPOINT: {(string.IsNullOrEmpty(Environment.GetEnvironmentVariable("MSI_ENDPOINT")) ? "[not set]" : "[set]")}");
        report.AppendLine();
    }

    private void AppendConnectionStringSection(StringBuilder report)
    {
        report.AppendLine("CONNECTION STRING");
        report.AppendLine("----------------------------------------");

        var connectionString = _configuration["DOTNET_CONNECTION_STRING"]
            ?? _configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            report.AppendLine("❌ DOTNET_CONNECTION_STRING is not set");
            report.AppendLine();
            return;
        }

        report.AppendLine("Status: ✅ Found");
        report.AppendLine($"Length: {connectionString.Length} characters");

        try
        {
            var builder = new SqlConnectionStringBuilder(connectionString);
            report.AppendLine($"Server: {builder.DataSource}");
            report.AppendLine($"Database: {builder.InitialCatalog}");

            if (builder.TryGetValue("Authentication", out var authentication))
            {
                report.AppendLine($"Authentication: {authentication}");
            }
            else if (!string.IsNullOrEmpty(builder.UserID))
            {
                report.AppendLine("Authentication: SQL username/password");
            }
            else
            {
                report.AppendLine("Authentication: (not specified - expect Managed Identity)");
            }
        }
        catch (Exception ex)
        {
            report.AppendLine($"❌ Failed to parse connection string: {ex.Message}");
        }

        report.AppendLine();
    }

    private async Task AppendEntityFrameworkProbe(StringBuilder report)
    {
        report.AppendLine("EF CORE PROBE");
        report.AppendLine("----------------------------------------");

        var database = await _dbContext.Database
            .SqlQueryRaw<string>("SELECT DB_NAME()")
            .FirstOrDefaultAsync();

        var menuGroupCount = await _dbContext.MenuGroups.CountAsync();
        var menuItemCount = await _dbContext.MenuItems.CountAsync();
        var powerBiCount = await _dbContext.PowerBIConfigs.CountAsync();

        report.AppendLine("✅ Successfully queried the database using EF Core");
        report.AppendLine($"Database: {database ?? "unknown"}");
        report.AppendLine($"MenuGroups: {menuGroupCount}");
        report.AppendLine($"MenuItems: {menuItemCount}");
        report.AppendLine($"PowerBIConfigs: {powerBiCount}");
        report.AppendLine();
    }

    private async Task AppendManagedIdentityProbe(StringBuilder report)
    {
        report.AppendLine("MANAGED IDENTITY PROBE");
        report.AppendLine("----------------------------------------");

        var connectionString = _configuration["DOTNET_CONNECTION_STRING"]
            ?? _configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            report.AppendLine("⏩ Skipped - connection string not configured");
            report.AppendLine();
            return;
        }

        var token = await _credential.GetTokenAsync(new TokenRequestContext(ManagedIdentityScopes), CancellationToken.None);
        report.AppendLine("✅ Managed identity access token acquired");
        report.AppendLine($"Token expires: {token.ExpiresOn:O}");

        await using var sqlConnection = new SqlConnection(connectionString);
        sqlConnection.AccessToken = token.Token;
        await sqlConnection.OpenAsync();
        report.AppendLine("✅ Successfully opened SQL connection using managed identity token");

        await using var command = sqlConnection.CreateCommand();
        command.CommandText = "SELECT SUSER_SNAME()";
        var identity = await command.ExecuteScalarAsync();
        report.AppendLine($"SQL Identity: {identity}");
        report.AppendLine();
    }
}
