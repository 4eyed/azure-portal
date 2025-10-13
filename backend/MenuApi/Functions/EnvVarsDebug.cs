using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MenuApi.Infrastructure;
using System.Text.Json;

namespace MenuApi.Functions;

/// <summary>
/// Debug endpoint for dumping all connection-related environment variables
/// </summary>
public class EnvVarsDebug
{
    private readonly ILogger<EnvVarsDebug> _logger;
    private readonly IConfiguration _configuration;

    public EnvVarsDebug(ILogger<EnvVarsDebug> logger, IConfiguration configuration)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    [Function("EnvVarsDebug")]
    public IActionResult Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "debug/env-vars")] HttpRequest req)
    {
        _logger.LogInformation("Environment variables debug dump requested");

        var isAzure = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME"));

        var result = new
        {
            timestamp = DateTime.UtcNow,
            environment = isAzure ? "Azure" : "Local",
            siteName = Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME"),
            variables = new
            {
                dotnetConnectionString = AnalyzeConnectionString(
                    _configuration["DOTNET_CONNECTION_STRING"]
                ),
                openfgaDatastoreUri = AnalyzeOpenFgaUri(
                    _configuration["OPENFGA_DATASTORE_URI"]
                ),
                openfgaStoreId = new
                {
                    exists = !string.IsNullOrEmpty(_configuration["OPENFGA_STORE_ID"]),
                    value = _configuration["OPENFGA_STORE_ID"]
                },
                openfgaDatastoreEngine = _configuration["OPENFGA_DATASTORE_ENGINE"],
                openfgaApiUrl = _configuration["OPENFGA_API_URL"],
                azureClientId = new
                {
                    exists = !string.IsNullOrEmpty(_configuration["AZURE_CLIENT_ID"]),
                    value = _configuration["AZURE_CLIENT_ID"]
                },
                azureTenantId = new
                {
                    exists = !string.IsNullOrEmpty(_configuration["AZURE_TENANT_ID"]),
                    value = _configuration["AZURE_TENANT_ID"]
                },
                functionsWorkerRuntime = Environment.GetEnvironmentVariable("FUNCTIONS_WORKER_RUNTIME"),
                websitesPort = Environment.GetEnvironmentVariable("WEBSITES_PORT"),
                azureWebJobsStorage = new
                {
                    exists = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AzureWebJobsStorage")),
                    length = Environment.GetEnvironmentVariable("AzureWebJobsStorage")?.Length ?? 0
                }
            }
        };

        return new CorsObjectResult(result)
        {
            StatusCode = 200
        };
    }

    private object AnalyzeConnectionString(string? connectionString)
    {
        if (string.IsNullOrEmpty(connectionString))
        {
            return new { exists = false };
        }

        try
        {
            var builder = new SqlConnectionStringBuilder(connectionString);

            // Sanitize password
            var sanitized = connectionString;
            if (!string.IsNullOrEmpty(builder.Password))
            {
                sanitized = System.Text.RegularExpressions.Regex.Replace(
                    sanitized,
                    @"(Password|Pwd)\s*=\s*[^;]+",
                    "$1=***REDACTED***",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase
                );
            }

            builder.TryGetValue("Authentication", out var auth);

            return new
            {
                exists = true,
                length = connectionString.Length,
                sanitized = sanitized,
                parsed = new
                {
                    server = builder.DataSource,
                    database = builder.InitialCatalog,
                    authentication = auth?.ToString() ?? "[not set]",
                    hasPassword = !string.IsNullOrEmpty(builder.Password),
                    encrypt = builder.Encrypt,
                    trustServerCertificate = builder.TrustServerCertificate,
                    connectionTimeout = builder.ConnectTimeout
                }
            };
        }
        catch (Exception ex)
        {
            return new
            {
                exists = true,
                length = connectionString.Length,
                error = ex.Message
            };
        }
    }

    private object AnalyzeOpenFgaUri(string? uri)
    {
        if (string.IsNullOrEmpty(uri))
        {
            return new { exists = false };
        }

        try
        {
            // Sanitize credentials
            var sanitized = System.Text.RegularExpressions.Regex.Replace(
                uri,
                @"://[^:]+:[^@]+@",
                "://***:***@"
            );

            // Extract fedauth parameter
            string? fedauth = null;
            var fedauthMatch = System.Text.RegularExpressions.Regex.Match(
                uri,
                @"fedauth=([^&;]+)",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
            );
            if (fedauthMatch.Success)
            {
                fedauth = fedauthMatch.Groups[1].Value;
            }

            // Extract server
            string? server = null;
            var serverMatch = System.Text.RegularExpressions.Regex.Match(
                uri,
                @"sqlserver://([^:/]+)"
            );
            if (serverMatch.Success)
            {
                server = serverMatch.Groups[1].Value;
            }

            // Extract database
            string? database = null;
            var dbMatch = System.Text.RegularExpressions.Regex.Match(
                uri,
                @"database=([^&;]+)",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
            );
            if (dbMatch.Success)
            {
                database = dbMatch.Groups[1].Value;
            }

            // Check for password in URI
            var hasPassword = System.Text.RegularExpressions.Regex.IsMatch(uri, @":[^:@]+@");

            return new
            {
                exists = true,
                length = uri.Length,
                sanitized = sanitized,
                parsed = new
                {
                    server = server,
                    database = database,
                    fedauth = fedauth ?? "[not set]",
                    hasPassword = hasPassword
                }
            };
        }
        catch (Exception ex)
        {
            return new
            {
                exists = true,
                length = uri.Length,
                error = ex.Message
            };
        }
    }
}
