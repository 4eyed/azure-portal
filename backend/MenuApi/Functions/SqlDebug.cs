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
using System.Text;

namespace MenuApi.Functions;

/// <summary>
/// Debug endpoint for testing SQL Server connectivity with various authentication methods
/// </summary>
public class SqlDebug
{
    private readonly ILogger<SqlDebug> _logger;
    private readonly IConfiguration _configuration;
    private readonly ApplicationDbContext _dbContext;

    public SqlDebug(ILogger<SqlDebug> logger, IConfiguration configuration, ApplicationDbContext dbContext)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    [Function("SqlDebug")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "debug/sql-test")] HttpRequest req)
    {
        _logger.LogInformation("SQL connectivity debug test requested");

        // Extract SQL token from request header (for local dev)
        req.ExtractAndStoreSqlToken(_logger);

        var results = new StringBuilder();
        results.AppendLine("========================================");
        results.AppendLine("SQL Server Connectivity Test Report");
        results.AppendLine($"Timestamp: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        results.AppendLine("========================================");
        results.AppendLine();

        // Environment detection
        var isAzure = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME"));
        var siteName = Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME") ?? "local";
        results.AppendLine($"Environment: {(isAzure ? "Azure" : "Local")}");
        results.AppendLine($"Site Name: {siteName}");
        results.AppendLine();

        // RAW Environment Variables Section
        results.AppendLine("========================================");
        results.AppendLine("RAW ENVIRONMENT VARIABLES (from IConfiguration)");
        results.AppendLine("========================================");
        results.AppendLine();

        // Show DOTNET_CONNECTION_STRING from environment
        var rawDotnetConnStr = _configuration["DOTNET_CONNECTION_STRING"];
        results.AppendLine("DOTNET_CONNECTION_STRING:");
        if (!string.IsNullOrEmpty(rawDotnetConnStr))
        {
            results.AppendLine($"  Status: ✅ SET");
            results.AppendLine($"  Length: {rawDotnetConnStr.Length} characters");
            results.AppendLine($"  Sanitized: {SanitizeConnectionString(rawDotnetConnStr)}");

            // Parse key properties
            try
            {
                var builder = new SqlConnectionStringBuilder(rawDotnetConnStr);
                results.AppendLine($"  Server: {builder.DataSource}");
                results.AppendLine($"  Database: {builder.InitialCatalog}");

                if (builder.TryGetValue("Authentication", out var auth))
                {
                    results.AppendLine($"  Authentication: {auth} ✅");
                }
                else
                {
                    results.AppendLine($"  Authentication: [NOT SET] ❌");
                }

                results.AppendLine($"  Has Password: {(!string.IsNullOrEmpty(builder.Password) ? "YES" : "NO")}");
                results.AppendLine($"  Encrypt: {builder.Encrypt}");
            }
            catch (Exception ex)
            {
                results.AppendLine($"  ❌ Failed to parse: {ex.Message}");
            }
        }
        else
        {
            results.AppendLine($"  Status: ❌ NOT SET");
        }
        results.AppendLine();

        // Show OPENFGA_DATASTORE_URI from environment
        var rawOpenFgaUri = _configuration["OPENFGA_DATASTORE_URI"];
        results.AppendLine("OPENFGA_DATASTORE_URI:");
        if (!string.IsNullOrEmpty(rawOpenFgaUri))
        {
            results.AppendLine($"  Status: ✅ SET");
            results.AppendLine($"  Length: {rawOpenFgaUri.Length} characters");

            // Sanitize credentials
            var sanitized = System.Text.RegularExpressions.Regex.Replace(
                rawOpenFgaUri,
                @"://[^:]+:[^@]+@",
                "://***:***@"
            );
            results.AppendLine($"  Sanitized: {sanitized}");

            // Check for fedauth
            if (rawOpenFgaUri.Contains("fedauth=", StringComparison.OrdinalIgnoreCase))
            {
                var fedauthMatch = System.Text.RegularExpressions.Regex.Match(
                    rawOpenFgaUri,
                    @"fedauth=([^&;]+)",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase
                );
                if (fedauthMatch.Success)
                {
                    results.AppendLine($"  fedauth: {fedauthMatch.Groups[1].Value} ✅");
                }
            }
            else
            {
                results.AppendLine($"  fedauth: [NOT SET] ❌");
            }
        }
        else
        {
            results.AppendLine($"  Status: ❌ NOT SET");
        }
        results.AppendLine();

        results.AppendLine("========================================");
        results.AppendLine();

        // Get connection string for testing
        var connectionString = _configuration["DOTNET_CONNECTION_STRING"]
            ?? _configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrEmpty(connectionString))
        {
            results.AppendLine("❌ ERROR: No connection string found!");
            return CreateResponse(results.ToString(), false);
        }

        // Test 1: Managed Identity (Azure production default)
        results.AppendLine("========================================");
        results.AppendLine("TEST 1: Managed Identity Connection");
        results.AppendLine("========================================");
        await TestManagedIdentity(connectionString, results);
        results.AppendLine();

        // Test 2: Username/Password (legacy fallback)
        results.AppendLine("========================================");
        results.AppendLine("TEST 2: Username/Password Connection");
        results.AppendLine("========================================");
        await TestUsernamePassword(connectionString, results);
        results.AppendLine();

        // Test 2b: Hardcoded SQL Auth (for Azure testing)
        var testHardcoded = req.Query["testHardcoded"].ToString()?.ToLower() == "true";
        if (testHardcoded)
        {
            results.AppendLine("========================================");
            results.AppendLine("TEST 2b: Hardcoded SQL Auth (Test Mode)");
            results.AppendLine("========================================");
            await TestHardcodedSqlAuth(results);
            results.AppendLine();
        }

        // Test 3: User SQL Token (like local dev)
        results.AppendLine("========================================");
        results.AppendLine("TEST 3: User SQL Token Connection");
        results.AppendLine("========================================");
        await TestUserSqlToken(connectionString, results);
        results.AppendLine();

        // Test 4: Direct SQL query test
        results.AppendLine("========================================");
        results.AppendLine("TEST 4: Direct SQL Query Test");
        results.AppendLine("========================================");
        await TestDirectQuery(connectionString, results);
        results.AppendLine();

        // Test 5: EF Core DbContext test
        results.AppendLine("========================================");
        results.AppendLine("TEST 5: EF Core DbContext Test");
        results.AppendLine("========================================");
        await TestEfCore(results);
        results.AppendLine();

        // Test 6: OpenFGA connectivity
        results.AppendLine("========================================");
        results.AppendLine("TEST 6: OpenFGA Connectivity Test");
        results.AppendLine("========================================");
        await TestOpenFga(results);
        results.AppendLine();

        results.AppendLine("========================================");
        results.AppendLine("Test Complete");
        results.AppendLine("========================================");

        return CreateResponse(results.ToString(), true);
    }

    private async Task TestManagedIdentity(string baseConnectionString, StringBuilder results)
    {
        try
        {
            // Build connection string with Managed Identity
            var builder = new SqlConnectionStringBuilder(baseConnectionString);
            builder.Remove("Password");
            builder.Remove("User ID");
            builder.Remove("UID");
            builder.Remove("PWD");
            builder["Authentication"] = "Active Directory Default";

            results.AppendLine($"Connection String: {SanitizeConnectionString(builder.ConnectionString)}");

            var stopwatch = Stopwatch.StartNew();
            using var connection = new SqlConnection(builder.ConnectionString);
            await connection.OpenAsync();
            stopwatch.Stop();

            results.AppendLine($"✅ SUCCESS - Connected in {stopwatch.ElapsedMilliseconds}ms");
            results.AppendLine($"   Server Version: {connection.ServerVersion}");
            results.AppendLine($"   Database: {connection.Database}");

            // Test a simple query
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT SUSER_SNAME() AS CurrentUser, DB_NAME() AS CurrentDB";
            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                results.AppendLine($"   Current User: {reader["CurrentUser"]}");
                results.AppendLine($"   Current DB: {reader["CurrentDB"]}");
            }

            _logger.LogInformation("✅ Managed Identity connection successful");
        }
        catch (Exception ex)
        {
            results.AppendLine($"❌ FAILED: {ex.Message}");
            results.AppendLine($"   Exception Type: {ex.GetType().Name}");
            if (ex.InnerException != null)
            {
                results.AppendLine($"   Inner Exception: {ex.InnerException.Message}");
            }
            _logger.LogError(ex, "❌ Managed Identity connection failed");
        }
    }

    private async Task TestUsernamePassword(string baseConnectionString, StringBuilder results)
    {
        try
        {
            // Check if connection string has username/password
            var builder = new SqlConnectionStringBuilder(baseConnectionString);
            var hasPassword = !string.IsNullOrEmpty(builder.Password);
            var hasUserId = !string.IsNullOrEmpty(builder.UserID);

            if (!hasPassword || !hasUserId)
            {
                results.AppendLine("⏩ SKIPPED: No username/password in connection string");
                return;
            }

            builder.Remove("Authentication");
            results.AppendLine($"Connection String: {SanitizeConnectionString(builder.ConnectionString)}");

            var stopwatch = Stopwatch.StartNew();
            using var connection = new SqlConnection(builder.ConnectionString);
            await connection.OpenAsync();
            stopwatch.Stop();

            results.AppendLine($"✅ SUCCESS - Connected in {stopwatch.ElapsedMilliseconds}ms");
            results.AppendLine($"   Server Version: {connection.ServerVersion}");
            results.AppendLine($"   Database: {connection.Database}");

            _logger.LogInformation("✅ Username/Password connection successful");
        }
        catch (Exception ex)
        {
            results.AppendLine($"❌ FAILED: {ex.Message}");
            results.AppendLine($"   Exception Type: {ex.GetType().Name}");
            _logger.LogError(ex, "❌ Username/Password connection failed");
        }
    }

    private async Task TestHardcodedSqlAuth(StringBuilder results)
    {
        results.AppendLine("🧪 Testing with hardcoded SQL Server credentials (from .env.azure-sql)");
        results.AppendLine("⚠️  This test uses embedded credentials - FOR TESTING ONLY!");
        results.AppendLine();

        // Hardcoded connection string from .env.azure-sql (line 31)
        var hardcodedConnStr = "Server=sqlsrv-menu-app-24259.database.windows.net;" +
                              "Database=db-menu-app;" +
                              "User Id=sqladmin;" +
                              "Password=P@ssw0rd1760128283!;" +
                              "Encrypt=true;" +
                              "TrustServerCertificate=false;";

        try
        {
            results.AppendLine($"Connection String: {SanitizeConnectionString(hardcodedConnStr)}");

            var stopwatch = Stopwatch.StartNew();
            using var connection = new SqlConnection(hardcodedConnStr);
            await connection.OpenAsync();
            stopwatch.Stop();

            results.AppendLine($"✅ SUCCESS - SQL Auth works! Connected in {stopwatch.ElapsedMilliseconds}ms");
            results.AppendLine($"   Server Version: {connection.ServerVersion}");
            results.AppendLine($"   Database: {connection.Database}");

            // Get current user info
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT SUSER_SNAME() AS CurrentUser, ORIGINAL_LOGIN() AS OriginalLogin";
            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                results.AppendLine($"   Current User: {reader["CurrentUser"]}");
                results.AppendLine($"   Original Login: {reader["OriginalLogin"]}");
            }

            results.AppendLine();
            results.AppendLine("✅ SQL Server connectivity is WORKING");
            results.AppendLine("   Issue is likely with Managed Identity configuration, NOT network/firewall");
            results.AppendLine();
            results.AppendLine("📋 Next steps:");
            results.AppendLine("   1. Verify Function App has Managed Identity enabled");
            results.AppendLine("   2. Check if Managed Identity is added as SQL user");
            results.AppendLine("   3. Verify connection string uses 'Authentication=Active Directory Default'");

            _logger.LogInformation("✅ Hardcoded SQL Auth connection successful");
        }
        catch (Exception ex)
        {
            results.AppendLine($"❌ FAILED: {ex.Message}");
            results.AppendLine($"   Exception Type: {ex.GetType().Name}");

            if (ex.Message.Contains("firewall", StringComparison.OrdinalIgnoreCase))
            {
                results.AppendLine();
                results.AppendLine("🔥 FIREWALL ISSUE DETECTED");
                results.AppendLine("   Check Azure SQL firewall rules");
                results.AppendLine("   Ensure 'Allow Azure services' is enabled");
            }
            else if (ex.Message.Contains("login failed", StringComparison.OrdinalIgnoreCase))
            {
                results.AppendLine();
                results.AppendLine("🔐 AUTHENTICATION FAILED");
                results.AppendLine("   SQL credentials may have changed");
                results.AppendLine("   Check .env.azure-sql for current password");
            }

            _logger.LogError(ex, "❌ Hardcoded SQL Auth connection failed");
        }
    }

    private async Task TestUserSqlToken(string connectionString, StringBuilder results)
    {
        try
        {
            // Get SQL token from AsyncLocal context (if set)
            var sqlToken = SqlTokenContext.SqlToken;

            if (string.IsNullOrEmpty(sqlToken))
            {
                results.AppendLine("⏩ SKIPPED: No SQL token in SqlTokenContext");
                return;
            }

            // Build connection string without authentication
            var builder = new SqlConnectionStringBuilder(connectionString);
            builder.Remove("Authentication");
            builder.Remove("Password");
            builder.Remove("User ID");

            results.AppendLine($"Connection String: {SanitizeConnectionString(builder.ConnectionString)}");
            results.AppendLine($"SQL Token: {sqlToken.Substring(0, Math.Min(20, sqlToken.Length))}...");

            var stopwatch = Stopwatch.StartNew();
            using var connection = new SqlConnection(builder.ConnectionString);
            connection.AccessToken = sqlToken;
            await connection.OpenAsync();
            stopwatch.Stop();

            results.AppendLine($"✅ SUCCESS - Connected in {stopwatch.ElapsedMilliseconds}ms");
            results.AppendLine($"   Server Version: {connection.ServerVersion}");
            results.AppendLine($"   Database: {connection.Database}");

            _logger.LogInformation("✅ User SQL Token connection successful");
        }
        catch (Exception ex)
        {
            results.AppendLine($"❌ FAILED: {ex.Message}");
            results.AppendLine($"   Exception Type: {ex.GetType().Name}");
            _logger.LogError(ex, "❌ User SQL Token connection failed");
        }
    }

    private async Task TestDirectQuery(string connectionString, StringBuilder results)
    {
        try
        {
            // Use the connection string as-is (will use whatever auth method is configured)
            results.AppendLine($"Connection String: {SanitizeConnectionString(connectionString)}");

            var stopwatch = Stopwatch.StartNew();
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT
                    @@VERSION AS SqlVersion,
                    SUSER_SNAME() AS CurrentUser,
                    DB_NAME() AS CurrentDB,
                    @@SERVERNAME AS ServerName,
                    GETUTCDATE() AS ServerTime";

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                stopwatch.Stop();
                results.AppendLine($"✅ SUCCESS - Query executed in {stopwatch.ElapsedMilliseconds}ms");
                results.AppendLine($"   Current User: {reader["CurrentUser"]}");
                results.AppendLine($"   Current DB: {reader["CurrentDB"]}");
                results.AppendLine($"   Server Name: {reader["ServerName"]}");
                results.AppendLine($"   Server Time: {reader["ServerTime"]}");
                results.AppendLine($"   SQL Version: {reader["SqlVersion"].ToString()?.Split('\n')[0]}");
            }

            _logger.LogInformation("✅ Direct SQL query successful");
        }
        catch (Exception ex)
        {
            results.AppendLine($"❌ FAILED: {ex.Message}");
            results.AppendLine($"   Exception Type: {ex.GetType().Name}");
            _logger.LogError(ex, "❌ Direct SQL query failed");
        }
    }

    private async Task TestEfCore(StringBuilder results)
    {
        try
        {
            results.AppendLine("Testing EF Core DbContext (actual application flow)...");

            var stopwatch = Stopwatch.StartNew();

            // Test database connectivity
            var canConnect = await _dbContext.Database.CanConnectAsync();
            stopwatch.Stop();

            if (!canConnect)
            {
                results.AppendLine($"❌ FAILED: Cannot connect to database");
                return;
            }

            results.AppendLine($"✅ Database connection: OK ({stopwatch.ElapsedMilliseconds}ms)");

            // Test a simple query
            stopwatch.Restart();
            var menuGroupCount = await _dbContext.MenuGroups.CountAsync();
            var menuItemCount = await _dbContext.MenuItems.CountAsync();
            stopwatch.Stop();

            results.AppendLine($"✅ Query execution: OK ({stopwatch.ElapsedMilliseconds}ms)");
            results.AppendLine($"   Menu Groups: {menuGroupCount}");
            results.AppendLine($"   Menu Items: {menuItemCount}");

            _logger.LogInformation("✅ EF Core DbContext test successful");
        }
        catch (Exception ex)
        {
            results.AppendLine($"❌ FAILED: {ex.Message}");
            results.AppendLine($"   Exception Type: {ex.GetType().Name}");
            if (ex.InnerException != null)
            {
                results.AppendLine($"   Inner Exception: {ex.InnerException.Message}");
            }
            _logger.LogError(ex, "❌ EF Core DbContext test failed");
        }
    }

    private async Task TestOpenFga(StringBuilder results)
    {
        try
        {
            var openFgaUrl = _configuration["OPENFGA_API_URL"] ?? "http://localhost:8080";
            results.AppendLine($"OpenFGA URL: {openFgaUrl}");

            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(5);

            var stopwatch = Stopwatch.StartNew();
            var response = await httpClient.GetAsync($"{openFgaUrl}/healthz");
            stopwatch.Stop();

            if (response.IsSuccessStatusCode)
            {
                results.AppendLine($"✅ SUCCESS - OpenFGA is reachable ({stopwatch.ElapsedMilliseconds}ms)");
                results.AppendLine($"   Status: {response.StatusCode}");

                // Test stores endpoint
                stopwatch.Restart();
                var storesResponse = await httpClient.GetAsync($"{openFgaUrl}/stores");
                stopwatch.Stop();

                if (storesResponse.IsSuccessStatusCode)
                {
                    var content = await storesResponse.Content.ReadAsStringAsync();
                    results.AppendLine($"   Stores API: OK ({stopwatch.ElapsedMilliseconds}ms)");
                    results.AppendLine($"   Response length: {content.Length} chars");
                }
            }
            else
            {
                results.AppendLine($"❌ FAILED: HTTP {response.StatusCode}");
            }

            _logger.LogInformation("✅ OpenFGA connectivity test completed");
        }
        catch (Exception ex)
        {
            results.AppendLine($"❌ FAILED: {ex.Message}");
            results.AppendLine($"   Exception Type: {ex.GetType().Name}");
            _logger.LogError(ex, "❌ OpenFGA connectivity test failed");
        }
    }

    private string SanitizeConnectionString(string connectionString)
    {
        if (string.IsNullOrEmpty(connectionString))
            return "[empty]";

        var builder = new SqlConnectionStringBuilder(connectionString);

        // Redact sensitive information
        if (!string.IsNullOrEmpty(builder.Password))
            builder.Password = "***REDACTED***";

        // Show partial user ID for debugging
        if (!string.IsNullOrEmpty(builder.UserID) && builder.UserID.Length > 4)
            builder.UserID = builder.UserID.Substring(0, 4) + "***";

        return builder.ConnectionString;
    }

    private IActionResult CreateResponse(string content, bool success)
    {
        return new CorsObjectResult(new
        {
            success = success,
            timestamp = DateTime.UtcNow,
            report = content
        })
        {
            StatusCode = success ? 200 : 500
        };
    }
}
