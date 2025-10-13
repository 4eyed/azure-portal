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
/// Function to create a new menu group
/// </summary>
public class CreateMenuGroup
{
    private readonly ILogger<CreateMenuGroup> _logger;
    private readonly IMenuService _menuService;
    private readonly IAuthorizationService _authService;
    private readonly IClaimsPrincipalParser _claimsParser;

    public CreateMenuGroup(
        ILogger<CreateMenuGroup> logger,
        IMenuService menuService,
        IAuthorizationService authService,
        IClaimsPrincipalParser claimsParser)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _menuService = menuService ?? throw new ArgumentNullException(nameof(menuService));
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        _claimsParser = claimsParser ?? throw new ArgumentNullException(nameof(claimsParser));
    }

    [Function("CreateMenuGroup")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "menu-groups")] HttpRequest req)
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
                    Error = "Only admins can create menu groups"
                })
                {
                    StatusCode = StatusCodes.Status403Forbidden
                };
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
