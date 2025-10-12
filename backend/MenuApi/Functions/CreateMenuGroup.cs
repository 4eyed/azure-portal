using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using MenuApi.Models.DTOs;
using MenuApi.Services;

namespace MenuApi.Functions;

/// <summary>
/// Function to create a new menu group
/// </summary>
public class CreateMenuGroup
{
    private readonly ILogger<CreateMenuGroup> _logger;
    private readonly IMenuService _menuService;
    private readonly IAuthorizationService _authService;

    public CreateMenuGroup(
        ILogger<CreateMenuGroup> logger,
        IMenuService menuService,
        IAuthorizationService authService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _menuService = menuService ?? throw new ArgumentNullException(nameof(menuService));
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
    }

    [Function("CreateMenuGroup")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "menu-groups")] HttpRequest req)
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
                    Error = "Only admins can create menu groups"
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

            _logger.LogInformation("Creating menu group: {GroupName}", request.Name);

            var result = await _menuService.CreateMenuGroup(request);

            return new CreatedResult($"/api/menu-groups/{result.Id}", result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating menu group");
            return new ObjectResult(new ErrorResponse
            {
                Error = "Failed to create menu group",
                Message = ex.Message
            })
            {
                StatusCode = StatusCodes.Status500InternalServerError
            };
        }
    }
}
