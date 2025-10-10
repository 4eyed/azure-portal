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
        _logger.LogInformation("GetMenu function processing request");

        // Get user from query parameter (in production, use proper authentication)
        var userId = req.Query["user"] ?? "alice";

        // Fetch menu items from database if available, otherwise use default
        var menuItems = _dbContext != null
            ? await _dbContext.MenuItems.ToListAsync()
            : new List<Models.MenuItem>
            {
                new() { Id = 1, Name = "Dashboard", Icon = "📊", Url = "/dashboard" },
                new() { Id = 2, Name = "Users", Icon = "👥", Url = "/users" },
                new() { Id = 3, Name = "Settings", Icon = "⚙️", Url = "/settings" },
                new() { Id = 4, Name = "Reports", Icon = "📈", Url = "/reports" }
            };

        var accessibleItems = new List<object>();

        foreach (var item in menuItems)
        {
            try
            {
                var checkRequest = new ClientCheckRequest
                {
                    User = $"user:{userId}",
                    Relation = "viewer",
                    Object = $"menu_item:{item.Name.ToLower()}"
                };

                var response = await _fgaClient.Check(checkRequest);

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
                _logger.LogError(ex, $"Error checking access for menu item {item.Id}");
            }
        }

        var httpResponse = req.CreateResponse(HttpStatusCode.OK);
        httpResponse.Headers.Add("Content-Type", "application/json");
        httpResponse.Headers.Add("Access-Control-Allow-Origin", "*");

        await httpResponse.WriteStringAsync(JsonSerializer.Serialize(new
        {
            user = userId,
            menuItems = accessibleItems
        }));

        return httpResponse;
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
