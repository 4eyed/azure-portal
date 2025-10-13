using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MenuApi.Data;
using MenuApi.Infrastructure;
using MenuApi.Extensions;
using System.Diagnostics;

namespace MenuApi.Functions;

/// <summary>
/// Function to check application health
/// </summary>
public class HealthCheck
{
    private readonly ILogger<HealthCheck> _logger;
    private readonly IConfiguration _configuration;
    private readonly ApplicationDbContext _dbContext;

    public HealthCheck(
        ILogger<HealthCheck> logger,
        IConfiguration configuration,
        ApplicationDbContext dbContext)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    [Function("HealthCheck")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health")] HttpRequest req)
    {
        var verbose = req.Query["verbose"].ToString()?.ToLower() == "true";

        _logger.LogInformation("Health check requested (verbose: {Verbose})", verbose);

        // Extract SQL token from request header (for local dev)
        req.ExtractAndStoreSqlToken(_logger);

        if (!verbose)
        {
            // Simple health check - just return OK
            return new CorsObjectResult(new
            {
                status = "healthy",
                timestamp = DateTime.UtcNow,
                version = "1.0.0"
            })
            {
                StatusCode = 200
            };
        }

        // Verbose health check - test all components
        var checks = new Dictionary<string, object>();
        var overallHealthy = true;

        // Check 1: Basic API health
        checks["api"] = new
        {
            status = "healthy",
            timestamp = DateTime.UtcNow,
            version = "1.0.0",
            environment = Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME") != null ? "Azure" : "Local"
        };

        // Check 2: Database connectivity
        var dbCheck = await CheckDatabase();
        checks["database"] = dbCheck;
        if (dbCheck.status != "healthy")
            overallHealthy = false;

        // Check 3: OpenFGA connectivity
        var openFgaCheck = await CheckOpenFga();
        checks["openfga"] = openFgaCheck;
        if (openFgaCheck.status != "healthy")
            overallHealthy = false;

        // Check 4: Configuration
        var configCheck = CheckConfiguration();
        checks["configuration"] = configCheck;
        if (configCheck.status != "healthy")
            overallHealthy = false;

        return new CorsObjectResult(new
        {
            status = overallHealthy ? "healthy" : "degraded",
            timestamp = DateTime.UtcNow,
            checks = checks
        })
        {
            StatusCode = overallHealthy ? 200 : 503
        };
    }

    private async Task<dynamic> CheckDatabase()
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            // Test database connection
            var canConnect = await _dbContext.Database.CanConnectAsync();
            stopwatch.Stop();

            if (!canConnect)
            {
                return new
                {
                    status = "unhealthy",
                    message = "Cannot connect to database",
                    responseTime = $"{stopwatch.ElapsedMilliseconds}ms"
                };
            }

            // Get connection info
            var connectionString = _configuration["DOTNET_CONNECTION_STRING"]
                ?? _configuration.GetConnectionString("DefaultConnection");

            string authMethod = "unknown";
            string server = "unknown";
            string database = "unknown";

            if (!string.IsNullOrEmpty(connectionString))
            {
                var builder = new SqlConnectionStringBuilder(connectionString);
                server = builder.DataSource;
                database = builder.InitialCatalog;

                if (builder.TryGetValue("Authentication", out var auth))
                {
                    authMethod = auth.ToString() ?? "unknown";
                }
                else if (!string.IsNullOrEmpty(builder.Password))
                {
                    authMethod = "SQL Authentication";
                }
            }

            // Test a simple query
            var menuGroupCount = await _dbContext.MenuGroups.CountAsync();
            stopwatch.Stop();

            return new
            {
                status = "healthy",
                server = server,
                database = database,
                authenticationMethod = authMethod,
                menuGroups = menuGroupCount,
                responseTime = $"{stopwatch.ElapsedMilliseconds}ms"
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Database health check failed");

            return new
            {
                status = "unhealthy",
                message = ex.Message,
                exceptionType = ex.GetType().Name,
                responseTime = $"{stopwatch.ElapsedMilliseconds}ms"
            };
        }
    }

    private async Task<dynamic> CheckOpenFga()
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var openFgaUrl = _configuration["OPENFGA_API_URL"] ?? "http://localhost:8080";
            var storeId = _configuration["OPENFGA_STORE_ID"];

            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(5);

            // Check health endpoint
            var healthResponse = await httpClient.GetAsync($"{openFgaUrl}/healthz");
            stopwatch.Stop();

            if (!healthResponse.IsSuccessStatusCode)
            {
                return new
                {
                    status = "unhealthy",
                    message = $"OpenFGA health check returned: {healthResponse.StatusCode}",
                    url = openFgaUrl,
                    responseTime = $"{stopwatch.ElapsedMilliseconds}ms"
                };
            }

            return new
            {
                status = "healthy",
                url = openFgaUrl,
                storeId = storeId ?? "not configured",
                responseTime = $"{stopwatch.ElapsedMilliseconds}ms"
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "OpenFGA health check failed");

            return new
            {
                status = "unhealthy",
                message = ex.Message,
                exceptionType = ex.GetType().Name,
                responseTime = $"{stopwatch.ElapsedMilliseconds}ms"
            };
        }
    }

    private dynamic CheckConfiguration()
    {
        var issues = new List<string>();

        // Check required configuration
        if (string.IsNullOrEmpty(_configuration["DOTNET_CONNECTION_STRING"]) &&
            string.IsNullOrEmpty(_configuration.GetConnectionString("DefaultConnection")))
        {
            issues.Add("No database connection string configured");
        }

        if (string.IsNullOrEmpty(_configuration["OPENFGA_API_URL"]))
        {
            issues.Add("OPENFGA_API_URL not configured");
        }

        if (string.IsNullOrEmpty(_configuration["OPENFGA_STORE_ID"]))
        {
            issues.Add("OPENFGA_STORE_ID not configured");
        }

        var isAzure = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME"));

        if (isAzure)
        {
            if (string.IsNullOrEmpty(_configuration["FUNCTIONS_WORKER_RUNTIME"]))
            {
                issues.Add("FUNCTIONS_WORKER_RUNTIME not set (should be 'dotnet-isolated')");
            }

            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AzureWebJobsStorage")))
            {
                issues.Add("AzureWebJobsStorage not configured");
            }
        }

        return new
        {
            status = issues.Count == 0 ? "healthy" : "degraded",
            environment = isAzure ? "Azure" : "Local",
            issues = issues.Count > 0 ? issues : null
        };
    }
}
