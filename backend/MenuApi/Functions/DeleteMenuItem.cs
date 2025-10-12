using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using MenuApi.Models.DTOs;
using MenuApi.Services;

namespace MenuApi.Functions;

/// <summary>
/// Function to delete a menu item
/// </summary>
public class DeleteMenuItem
{
    private readonly ILogger<DeleteMenuItem> _logger;
    private readonly IMenuService _menuService;
    private readonly IAuthorizationService _authService;

    public DeleteMenuItem(
        ILogger<DeleteMenuItem> logger,
        IMenuService menuService,
        IAuthorizationService authService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _menuService = menuService ?? throw new ArgumentNullException(nameof(menuService));
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
    }

    [Function("DeleteMenuItem")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "menu-items/{id:int}")] HttpRequest req,
        int id)
    {
        try
        {
            var userId = req.Query["user"].ToString();
            if (string.IsNullOrEmpty(userId))
            {
                return new BadRequestObjectResult(new ErrorResponse
                {
                    Error = "User parameter is required"
                });
            }

            // Check if user is admin
            if (!await _authService.IsAdmin(userId))
            {
                return new UnauthorizedObjectResult(new ErrorResponse
                {
                    Error = "Only admins can delete menu items"
                });
            }

            _logger.LogInformation("Deleting menu item: {ItemId}", id);

            var result = await _menuService.DeleteMenuItem(id);

            if (!result)
            {
                return new NotFoundObjectResult(new ErrorResponse
                {
                    Error = $"Menu item with ID {id} not found"
                });
            }

            return new NoContentResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting menu item {ItemId}", id);
            return new ObjectResult(new ErrorResponse
            {
                Error = "Failed to delete menu item",
                Message = ex.Message
            })
            {
                StatusCode = StatusCodes.Status500InternalServerError
            };
        }
    }
}
