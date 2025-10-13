using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MenuApi.Infrastructure;
using MenuApi.Extensions;
using System.Text;

namespace MenuApi.Functions;

/// <summary>
/// Debug endpoint for inspecting configuration and environment
/// </summary>
public class ConfigDebug
{
    private readonly ILogger<ConfigDebug> _logger;
    private readonly IConfiguration _configuration;

    public ConfigDebug(ILogger<ConfigDebug> logger, IConfiguration configuration)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    [Function("ConfigDebug")]
    public IActionResult Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "debug/config")] HttpRequest req)
    {
        _logger.LogInformation("Configuration debug requested");

        // Extract SQL token from request header (for local dev)
        req.ExtractAndStoreSqlToken(_logger);

        var results = new StringBuilder();
        results.AppendLine("========================================");
        results.AppendLine("Configuration Diagnostics");
        results.AppendLine($"Timestamp: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        results.AppendLine("========================================");
        results.AppendLine();

        // Environment Detection
        results.AppendLine("ENVIRONMENT DETECTION");
        results.AppendLine("----------------------------------------");
        var isAzure = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME"));
        results.AppendLine($"Is Azure: {isAzure}");
        results.AppendLine($"Site Name: {Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME") ?? "not set"}");
        results.AppendLine($"Hostname: {Environment.MachineName}");
        results.AppendLine($"OS: {Environment.OSVersion}");
        results.AppendLine($"Framework: {Environment.Version}");
        results.AppendLine();

        // Azure Function Configuration
        results.AppendLine("AZURE FUNCTIONS CONFIGURATION");
        results.AppendLine("----------------------------------------");
        AppendEnvVar(results, "FUNCTIONS_WORKER_RUNTIME");
        AppendEnvVar(results, "FUNCTIONS_EXTENSION_VERSION");
        AppendEnvVar(results, "AzureWebJobsScriptRoot");
        AppendEnvVar(results, "AzureWebJobsStorage", sanitize: true);
        AppendEnvVar(results, "WEBSITES_PORT");
        AppendEnvVar(results, "WEBSITES_ENABLE_APP_SERVICE_STORAGE");
        AppendEnvVar(results, "WEBSITES_HEALTHCHECK_MAXPINGFAILURES");
        results.AppendLine();

        // Database Configuration
        results.AppendLine("DATABASE CONFIGURATION");
        results.AppendLine("----------------------------------------");
        var dotnetConnStr = _configuration["DOTNET_CONNECTION_STRING"];
        var defaultConnStr = _configuration.GetConnectionString("DefaultConnection");

        if (!string.IsNullOrEmpty(dotnetConnStr))
        {
            results.AppendLine("DOTNET_CONNECTION_STRING: [SET]");
            AnalyzeConnectionString(results, dotnetConnStr);
        }
        else if (!string.IsNullOrEmpty(defaultConnStr))
        {
            results.AppendLine("DefaultConnection: [SET]");
            AnalyzeConnectionString(results, defaultConnStr);
        }
        else
        {
            results.AppendLine("❌ No connection string found!");
        }
        results.AppendLine();

        // OpenFGA Configuration
        results.AppendLine("OPENFGA CONFIGURATION");
        results.AppendLine("----------------------------------------");
        var openFgaApiUrl = _configuration["OPENFGA_API_URL"];
        var openFgaStoreId = _configuration["OPENFGA_STORE_ID"];
        var openFgaDatastoreUri = _configuration["OPENFGA_DATASTORE_URI"];
        var openFgaEngine = _configuration["OPENFGA_DATASTORE_ENGINE"];

        results.AppendLine($"OPENFGA_API_URL: {openFgaApiUrl ?? "[not set]"}");
        results.AppendLine($"OPENFGA_STORE_ID: {openFgaStoreId ?? "[not set]"}");
        results.AppendLine($"OPENFGA_DATASTORE_ENGINE: {openFgaEngine ?? "[not set]"}");

        if (!string.IsNullOrEmpty(openFgaDatastoreUri))
        {
            results.AppendLine("OPENFGA_DATASTORE_URI: [SET]");
            AnalyzeConnectionString(results, openFgaDatastoreUri, prefix: "  ");
        }
        else
        {
            results.AppendLine("OPENFGA_DATASTORE_URI: [not set]");
        }
        results.AppendLine();

        // Azure AD / Authentication Configuration
        results.AppendLine("AZURE AD CONFIGURATION");
        results.AppendLine("----------------------------------------");
        AppendConfigVar(results, "AZURE_CLIENT_ID");
        AppendConfigVar(results, "AZURE_TENANT_ID");
        AppendConfigVar(results, "AZURE_CLIENT_SECRET", sanitize: true);
        results.AppendLine();

        // Process Information
        results.AppendLine("PROCESS INFORMATION");
        results.AppendLine("----------------------------------------");
        results.AppendLine($"Process ID: {Environment.ProcessId}");
        results.AppendLine($"Working Directory: {Environment.CurrentDirectory}");
        results.AppendLine($"Processor Count: {Environment.ProcessorCount}");
        results.AppendLine($"Memory (MB): {Environment.WorkingSet / 1024 / 1024}");
        results.AppendLine();

        // SQL Token Context
        results.AppendLine("SQL TOKEN CONTEXT");
        results.AppendLine("----------------------------------------");
        var sqlToken = SqlTokenContext.SqlToken;
        if (!string.IsNullOrEmpty(sqlToken))
        {
            results.AppendLine($"SQL Token: {sqlToken.Substring(0, Math.Min(20, sqlToken.Length))}... ({sqlToken.Length} chars)");
        }
        else
        {
            results.AppendLine("SQL Token: [not set]");
        }
        results.AppendLine();

        // OpenFGA Process Check
        results.AppendLine("OPENFGA PROCESS CHECK");
        results.AppendLine("----------------------------------------");
        CheckOpenFgaProcess(results);
        results.AppendLine();

        results.AppendLine("========================================");
        results.AppendLine("End of Configuration Report");
        results.AppendLine("========================================");

        return new CorsObjectResult(new
        {
            success = true,
            timestamp = DateTime.UtcNow,
            report = results.ToString()
        })
        {
            StatusCode = 200
        };
    }

    private void AppendEnvVar(StringBuilder sb, string varName, bool sanitize = false)
    {
        var value = Environment.GetEnvironmentVariable(varName);
        if (string.IsNullOrEmpty(value))
        {
            sb.AppendLine($"{varName}: [not set]");
        }
        else if (sanitize)
        {
            sb.AppendLine($"{varName}: [SET - {value.Length} chars]");
        }
        else
        {
            sb.AppendLine($"{varName}: {value}");
        }
    }

    private void AppendConfigVar(StringBuilder sb, string key, bool sanitize = false)
    {
        var value = _configuration[key];
        if (string.IsNullOrEmpty(value))
        {
            sb.AppendLine($"{key}: [not set]");
        }
        else if (sanitize)
        {
            sb.AppendLine($"{key}: [SET - {value.Length} chars]");
        }
        else
        {
            sb.AppendLine($"{key}: {value}");
        }
    }

    private void AnalyzeConnectionString(StringBuilder sb, string connectionString, string prefix = "  ")
    {
        try
        {
            var builder = new SqlConnectionStringBuilder(connectionString);

            sb.AppendLine($"{prefix}Server: {builder.DataSource}");
            sb.AppendLine($"{prefix}Database: {builder.InitialCatalog}");
            sb.AppendLine($"{prefix}User ID: {(string.IsNullOrEmpty(builder.UserID) ? "[not set]" : builder.UserID.Length > 4 ? builder.UserID.Substring(0, 4) + "***" : "***")}");
            sb.AppendLine($"{prefix}Password: {(string.IsNullOrEmpty(builder.Password) ? "[not set]" : "[SET]")}");

            // Check authentication method
            if (builder.TryGetValue("Authentication", out var authMethod))
            {
                sb.AppendLine($"{prefix}Authentication: {authMethod}");
            }
            else
            {
                sb.AppendLine($"{prefix}Authentication: [default - SQL auth]");
            }

            // Additional properties
            sb.AppendLine($"{prefix}Encrypt: {builder.Encrypt}");
            sb.AppendLine($"{prefix}Trust Server Certificate: {builder.TrustServerCertificate}");
            sb.AppendLine($"{prefix}Connection Timeout: {builder.ConnectTimeout}s");

            // Analyze authentication method
            if (authMethod != null && authMethod.ToString().Contains("ActiveDirectory", StringComparison.OrdinalIgnoreCase))
            {
                sb.AppendLine($"{prefix}🔐 Uses Managed Identity / Azure AD");
            }
            else if (!string.IsNullOrEmpty(builder.Password))
            {
                sb.AppendLine($"{prefix}🔑 Uses username/password");
            }
            else
            {
                sb.AppendLine($"{prefix}⚠️  No clear authentication method detected");
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine($"{prefix}❌ Failed to parse connection string: {ex.Message}");
        }
    }

    private void CheckOpenFgaProcess(StringBuilder sb)
    {
        try
        {
            // Try to check if OpenFGA is running by checking the health endpoint
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(2);

            var openFgaUrl = _configuration["OPENFGA_API_URL"] ?? "http://localhost:8080";
            var healthCheckTask = httpClient.GetAsync($"{openFgaUrl}/healthz");

            if (healthCheckTask.Wait(TimeSpan.FromSeconds(2)))
            {
                var response = healthCheckTask.Result;
                if (response.IsSuccessStatusCode)
                {
                    sb.AppendLine($"✅ OpenFGA is running and responding");
                    sb.AppendLine($"   Health endpoint: {openFgaUrl}/healthz");
                }
                else
                {
                    sb.AppendLine($"⚠️  OpenFGA responded with: {response.StatusCode}");
                }
            }
            else
            {
                sb.AppendLine($"⏱️  OpenFGA health check timed out");
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine($"❌ Cannot reach OpenFGA: {ex.Message}");
        }
    }
}
