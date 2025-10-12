using MenuApi.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using MenuApi.Models.DTOs;
using MenuApi.Services;

namespace MenuApi.Functions;

/// <summary>
/// Function to retrieve menu structure filtered by user permissions
/// </summary>
public class GetMenuStructure
{
    private readonly ILogger<GetMenuStructure> _logger;
    private readonly IMenuService _menuService;

    public GetMenuStructure(ILogger<GetMenuStructure> logger, IMenuService menuService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _menuService = menuService ?? throw new ArgumentNullException(nameof(menuService));
    }

    [Function("GetMenuStructure")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "menu-structure")] HttpRequest req)
    {
        try
        {
            var userId = req.Query["user"].ToString();
            if (string.IsNullOrEmpty(userId))
            {
                return new CorsObjectResult(new ErrorResponse
                {
                    Error = "User parameter is required"
                });
            }

            _logger.LogInformation("Fetching menu structure for user: {UserId}", userId);

            var result = await _menuService.GetMenuStructure(userId);

            return new CorsObjectResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching menu structure");
            return new CorsObjectResult(new ErrorResponse
            {
                Error = "Failed to fetch menu structure",
                Message = ex.Message
            })
            {
                StatusCode = StatusCodes.Status500InternalServerError
            };
        }
    }
}
