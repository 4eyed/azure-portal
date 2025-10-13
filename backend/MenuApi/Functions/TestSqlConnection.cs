using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MenuApi.Data;
using MenuApi.Extensions;
using MenuApi.Infrastructure;

namespace MenuApi.Functions;

/// <summary>
/// Function to test SQL Server database connectivity and verify configuration
/// </summary>
public class TestSqlConnection
{
    private readonly ILogger<TestSqlConnection> _logger;
    private readonly ApplicationDbContext _dbContext;

    public TestSqlConnection(
        ILogger<TestSqlConnection> logger,
        ApplicationDbContext dbContext)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    [Function("TestSqlConnection")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "test-sql")] HttpRequest req)
    {
        try
        {
            // Extract SQL token from request header and store in AsyncLocal context
            // This allows the DbContext to authenticate using the user's SQL token
            req.ExtractAndStoreSqlToken(_logger);

            _logger.LogInformation("Testing SQL Server connectivity...");

            // Test 1: Get database name
            var databaseName = await _dbContext.Database
                .SqlQueryRaw<string>("SELECT DB_NAME() AS Value")
                .FirstOrDefaultAsync();

            _logger.LogInformation("Connected to database: {DatabaseName}", databaseName);

            // Test 2: Count tables
            var menuItemsCount = await _dbContext.MenuItems.CountAsync();
            var menuGroupsCount = await _dbContext.MenuGroups.CountAsync();
            var powerBIConfigsCount = await _dbContext.PowerBIConfigs.CountAsync();

            _logger.LogInformation(
                "Table counts - MenuItems: {MenuItemsCount}, MenuGroups: {MenuGroupsCount}, PowerBIConfigs: {PowerBIConfigsCount}",
                menuItemsCount, menuGroupsCount, powerBIConfigsCount);

            // Test 3: Get current SQL user information
            var currentUser = await _dbContext.Database
                .SqlQueryRaw<string>("SELECT CURRENT_USER AS Value")
                .FirstOrDefaultAsync();

            var loginName = await _dbContext.Database
                .SqlQueryRaw<string>("SELECT SUSER_NAME() AS Value")
                .FirstOrDefaultAsync();

            _logger.LogInformation(
                "SQL User - Current: {CurrentUser}, Login: {LoginName}",
                currentUser, loginName);

            // Get connection string details (safe - no password)
            var connectionString = _dbContext.Database.GetConnectionString();
            var serverName = connectionString?.Split(';')
                .FirstOrDefault(s => s.StartsWith("Server=", StringComparison.OrdinalIgnoreCase))
                ?.Split('=').LastOrDefault();

            var response = new
            {
                success = true,
                database = databaseName ?? "unknown",
                tablesFound = new
                {
                    menuItems = menuItemsCount,
                    menuGroups = menuGroupsCount,
                    powerBIConfigs = powerBIConfigsCount
                },
                environment = new
                {
                    isAzure = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME")),
                    websiteSiteName = Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME") ?? "not-set",
                    azureFunctionsEnvironment = Environment.GetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT") ?? "not-set",
                    hasSqlToken = !string.IsNullOrEmpty(SqlTokenContext.SqlToken),
                    sqlTokenLength = SqlTokenContext.SqlToken?.Length ?? 0
                },
                connectionInfo = new
                {
                    server = serverName ?? "unknown",
                    database = databaseName ?? "unknown",
                    authMethod = string.IsNullOrEmpty(SqlTokenContext.SqlToken) ? "Managed Identity / Connection String" : "User SQL Token",
                    connectionOpen = _dbContext.Database.CanConnect(),
                    efCoreVersion = "8.0",
                    providerName = _dbContext.Database.ProviderName
                },
                user = new
                {
                    currentUser = currentUser ?? "unknown",
                    loginName = loginName ?? "unknown"
                },
                timestamp = DateTime.UtcNow
            };

            return new CorsObjectResult(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SQL connectivity test failed");

            var errorResponse = new
            {
                success = false,
                error = ex.Message,
                errorType = ex.GetType().Name,
                timestamp = DateTime.UtcNow
            };

            return new CorsObjectResult(errorResponse)
            {
                StatusCode = StatusCodes.Status500InternalServerError
            };
        }
    }
}
