using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MenuApi.Data;
using OpenFga.Sdk.Client;
using OpenFga.Sdk.Client.Model;
using System.Net;
using System.Text.Json;

namespace MenuApi;

public class MenuFunction
{
    private readonly ILogger _logger;
    private readonly OpenFgaClient _fgaClient;
    private readonly ApplicationDbContext? _dbContext;

    public MenuFunction(ILoggerFactory loggerFactory, OpenFgaClient fgaClient, ApplicationDbContext? dbContext = null)
    {
        _logger = loggerFactory.CreateLogger<MenuFunction>();
        _fgaClient = fgaClient;
        _dbContext = dbContext;
    }

    [Function("GetMenu")]
    public async Task<HttpResponseData> GetMenu(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "menu")] HttpRequestData req)
    {
        try
        {
            _logger.LogInformation("=== GetMenu function started ===");
            _logger.LogInformation($"OpenFGA Client StoreId: {_fgaClient.StoreId}");

            // Get user from query parameter (in production, use proper authentication)
            var userId = req.Query["user"] ?? "alice";
            _logger.LogInformation($"Processing request for user: {userId}");

            // Fetch menu items from database - REQUIRED
            if (_dbContext == null)
            {
                _logger.LogError("❌ Database context is not configured. DOTNET_CONNECTION_STRING must be set.");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                errorResponse.Headers.Add("Content-Type", "application/json");
                errorResponse.Headers.Add("Access-Control-Allow-Origin", "*");
                await errorResponse.WriteStringAsync(JsonSerializer.Serialize(new
                {
                    error = "Database not configured",
                    message = "DOTNET_CONNECTION_STRING environment variable is required"
                }));
                return errorResponse;
            }

            var menuItems = await _dbContext.MenuItems.ToListAsync();
            _logger.LogInformation($"Found {menuItems.Count} menu items to check");

            var accessibleItems = new List<object>();

            foreach (var item in menuItems)
            {
                try
                {
                    _logger.LogInformation($"Checking access for item: {item.Name}");

                    var checkRequest = new ClientCheckRequest
                    {
                        User = $"user:{userId}",
                        Relation = "viewer",
                        Object = $"menu_item:{item.Name.ToLower()}"
                    };

                    _logger.LogInformation($"Making OpenFGA Check request: User={checkRequest.User}, Relation={checkRequest.Relation}, Object={checkRequest.Object}");

                    var response = await _fgaClient.Check(checkRequest);

                    _logger.LogInformation($"OpenFGA Check response for {item.Name}: Allowed={response.Allowed}");

                    if (response.Allowed == true)
                    {
                        accessibleItems.Add(new
                        {
                            Id = item.Id,
                            Name = item.Name,
                            Icon = item.Icon,
                            Url = item.Url,
                            Description = item.Description
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"❌ Error checking access for menu item {item.Id} ({item.Name}): {ex.Message}");
                    _logger.LogError($"Stack trace: {ex.StackTrace}");
                }
            }

            _logger.LogInformation($"Returning {accessibleItems.Count} accessible items for user {userId}");

            var httpResponse = req.CreateResponse(HttpStatusCode.OK);
            httpResponse.Headers.Add("Content-Type", "application/json");
            httpResponse.Headers.Add("Access-Control-Allow-Origin", "*");

            await httpResponse.WriteStringAsync(JsonSerializer.Serialize(new
            {
                user = userId,
                menuItems = accessibleItems
            }));

            _logger.LogInformation("=== GetMenu function completed successfully ===");
            return httpResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"❌ FATAL ERROR in GetMenu: {ex.Message}");
            _logger.LogError($"Stack trace: {ex.StackTrace}");

            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            errorResponse.Headers.Add("Content-Type", "application/json");
            errorResponse.Headers.Add("Access-Control-Allow-Origin", "*");

            await errorResponse.WriteStringAsync(JsonSerializer.Serialize(new
            {
                error = "Internal server error",
                message = ex.Message,
                details = ex.ToString()
            }));

            return errorResponse;
        }
    }

    [Function("GetMenuItem")]
    public async Task<HttpResponseData> GetMenuItem(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "menu/{id}")] HttpRequestData req,
        string id)
    {
        _logger.LogInformation($"GetMenuItem function processing request for {id}");

        var userId = req.Query["user"] ?? "alice";

        var checkRequest = new ClientCheckRequest
        {
            User = $"user:{userId}",
            Relation = "viewer",
            Object = $"menu_item:{id}"
        };

        var response = await _fgaClient.Check(checkRequest);

        if (response.Allowed != true)
        {
            var forbiddenResponse = req.CreateResponse(HttpStatusCode.Forbidden);
            forbiddenResponse.Headers.Add("Access-Control-Allow-Origin", "*");
            await forbiddenResponse.WriteStringAsync(JsonSerializer.Serialize(new
            {
                error = "Access denied"
            }));
            return forbiddenResponse;
        }

        var httpResponse = req.CreateResponse(HttpStatusCode.OK);
        httpResponse.Headers.Add("Content-Type", "application/json");
        httpResponse.Headers.Add("Access-Control-Allow-Origin", "*");

        await httpResponse.WriteStringAsync(JsonSerializer.Serialize(new
        {
            id,
            user = userId,
            message = $"You have access to {id}"
        }));

        return httpResponse;
    }
}
