using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using MenuApi.Models.DTOs;
using MenuApi.Services;

namespace MenuApi.Functions;

/// <summary>
/// Function to update an existing menu group
/// </summary>
public class UpdateMenuGroup
{
    private readonly ILogger<UpdateMenuGroup> _logger;
    private readonly IMenuService _menuService;
    private readonly IAuthorizationService _authService;

    public UpdateMenuGroup(
        ILogger<UpdateMenuGroup> logger,
        IMenuService menuService,
        IAuthorizationService authService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _menuService = menuService ?? throw new ArgumentNullException(nameof(menuService));
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
    }

    [Function("UpdateMenuGroup")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "menu-groups/{id:int}")] HttpRequest req,
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
                    Error = "Only admins can update menu groups"
                });
            }

            var body = await new StreamReader(req.Body).ReadToEndAsync();
            var request = JsonSerializer.Deserialize<MenuGroupRequest>(body, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (request == null)
            {
                return new BadRequestObjectResult(new ErrorResponse
                {
                    Error = "Invalid menu group data"
                });
            }

            _logger.LogInformation("Updating menu group: {GroupId}", id);

            var result = await _menuService.UpdateMenuGroup(id, request);

            if (result == null)
            {
                return new NotFoundObjectResult(new ErrorResponse
                {
                    Error = $"Menu group with ID {id} not found"
                });
            }

            return new OkObjectResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating menu group {GroupId}", id);
            return new ObjectResult(new ErrorResponse
            {
                Error = "Failed to update menu group",
                Message = ex.Message
            })
            {
                StatusCode = StatusCodes.Status500InternalServerError
            };
        }
    }
}
