using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using MenuApi.Models.DTOs;
using MenuApi.Services;
using MenuApi.Extensions;

namespace MenuApi.Functions;

/// <summary>
/// Function to delete a menu group
/// </summary>
public class DeleteMenuGroup
{
    private readonly ILogger<DeleteMenuGroup> _logger;
    private readonly IMenuService _menuService;
    private readonly IAuthorizationService _authService;
    private readonly IClaimsPrincipalParser _claimsParser;

    public DeleteMenuGroup(
        ILogger<DeleteMenuGroup> logger,
        IMenuService menuService,
        IAuthorizationService authService,
        IClaimsPrincipalParser claimsParser)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _menuService = menuService ?? throw new ArgumentNullException(nameof(menuService));
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        _claimsParser = claimsParser ?? throw new ArgumentNullException(nameof(claimsParser));
    }

    [Function("DeleteMenuGroup")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "menu-groups/{id:int}")] HttpRequest req,
        int id)
    {
        try
        {
            using var sqlTokenScope = req.BeginSqlTokenScope(_logger);

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
                    Error = "Only admins can delete menu groups"
                })
                {
                    StatusCode = StatusCodes.Status403Forbidden
                };
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
