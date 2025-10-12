using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using MenuApi.Models.DTOs;
using MenuApi.Services;
using MenuApi.Extensions;

namespace MenuApi.Functions;

/// <summary>
/// Function to update an existing menu item
/// </summary>
public class UpdateMenuItem
{
    private readonly ILogger<UpdateMenuItem> _logger;
    private readonly IMenuService _menuService;
    private readonly IAuthorizationService _authService;
    private readonly IClaimsPrincipalParser _claimsParser;

    public UpdateMenuItem(
        ILogger<UpdateMenuItem> logger,
        IMenuService menuService,
        IAuthorizationService authService,
        IClaimsPrincipalParser claimsParser)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _menuService = menuService ?? throw new ArgumentNullException(nameof(menuService));
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        _claimsParser = claimsParser ?? throw new ArgumentNullException(nameof(claimsParser));
    }

    [Function("UpdateMenuItem")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "menu-items/{id:int}")] HttpRequest req,
        int id)
    {
        try
        {
            // Extract authenticated user ID
            var userId = req.GetAuthenticatedUserId(_claimsParser);
            if (string.IsNullOrEmpty(userId))
            {
                return new UnauthorizedObjectResult(new ErrorResponse
                {
                    Error = "User is not authenticated"
                });
            }

            // Check if user is admin
            if (!req.IsAdmin(_claimsParser) && !await _authService.IsAdmin(userId))
            {
                return new ObjectResult(new ErrorResponse
                {
                    Error = "Only admins can update menu items"
                })
                {
                    StatusCode = StatusCodes.Status403Forbidden
                };
            }

            var body = await new StreamReader(req.Body).ReadToEndAsync();
            var request = JsonSerializer.Deserialize<MenuItemRequest>(body, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (request == null)
            {
                return new BadRequestObjectResult(new ErrorResponse
                {
                    Error = "Invalid menu item data"
                });
            }

            _logger.LogInformation("Updating menu item: {ItemId}", id);

            var result = await _menuService.UpdateMenuItem(id, request);

            if (result == null)
            {
                return new NotFoundObjectResult(new ErrorResponse
                {
                    Error = $"Menu item with ID {id} not found"
                });
            }

            return new OkObjectResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating menu item {ItemId}", id);
            return new ObjectResult(new ErrorResponse
            {
                Error = "Failed to update menu item",
                Message = ex.Message
            })
            {
                StatusCode = StatusCodes.Status500InternalServerError
            };
        }
    }
}
