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
    private readonly IClaimsPrincipalParser _claimsParser;

    public GetMenuStructure(ILogger<GetMenuStructure> logger, IMenuService menuService, IClaimsPrincipalParser claimsParser)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _menuService = menuService ?? throw new ArgumentNullException(nameof(menuService));
        _claimsParser = claimsParser ?? throw new ArgumentNullException(nameof(claimsParser));
    }

    [Function("GetMenuStructure")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "menu-structure")] HttpRequest req)
    {
        try
        {
            // Extract user ID from X-MS-CLIENT-PRINCIPAL header (injected by Azure Static Web Apps)
            var userId = _claimsParser.GetUserId(req);

            // Fallback to query parameter for local development
            if (string.IsNullOrEmpty(userId))
            {
                userId = req.Query["user"].ToString();
            }

            if (string.IsNullOrEmpty(userId))
            {
                return new CorsObjectResult(new ErrorResponse
                {
                    Error = "User is not authenticated"
                })
                {
                    StatusCode = StatusCodes.Status401Unauthorized
                };
            }

            _logger.LogInformation("Fetching menu structure for user: {UserId}", userId);

            var result = await _menuService.GetMenuStructure(userId);

            // Add caching headers to reduce API calls (5 minutes cache)
            var response = new CorsObjectResult(result);
            req.HttpContext.Response.Headers["Cache-Control"] = "private, max-age=300";

            return response;
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
