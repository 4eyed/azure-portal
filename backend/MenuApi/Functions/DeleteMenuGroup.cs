using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using MenuApi.Models.DTOs;
using MenuApi.Services;

namespace MenuApi.Functions;

/// <summary>
/// Function to delete a menu group
/// </summary>
public class DeleteMenuGroup
{
    private readonly ILogger<DeleteMenuGroup> _logger;
    private readonly IMenuService _menuService;
    private readonly IAuthorizationService _authService;

    public DeleteMenuGroup(
        ILogger<DeleteMenuGroup> logger,
        IMenuService menuService,
        IAuthorizationService authService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _menuService = menuService ?? throw new ArgumentNullException(nameof(menuService));
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
    }

    [Function("DeleteMenuGroup")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "menu-groups/{id:int}")] HttpRequest req,
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
                    Error = "Only admins can delete menu groups"
                });
            }

            _logger.LogInformation("Deleting menu group: {GroupId}", id);

            var success = await _menuService.DeleteMenuGroup(id);

            if (!success)
            {
                return new NotFoundObjectResult(new ErrorResponse
                {
                    Error = $"Menu group with ID {id} not found"
                });
            }

            return new NoContentResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting menu group {GroupId}", id);
            return new ObjectResult(new ErrorResponse
            {
                Error = "Failed to delete menu group",
                Message = ex.Message
            })
            {
                StatusCode = StatusCodes.Status500InternalServerError
            };
        }
    }
}
