using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using MenuApi.Services;
using MenuApi.Extensions;

namespace MenuApi.Functions;

/// <summary>
/// Function to check if the authenticated user is an admin
/// </summary>
public class CheckAdmin
{
    private readonly ILogger<CheckAdmin> _logger;
    private readonly IAuthorizationService _authService;
    private readonly IClaimsPrincipalParser _claimsParser;

    public CheckAdmin(
        ILogger<CheckAdmin> logger,
        IAuthorizationService authService,
        IClaimsPrincipalParser claimsParser)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        _claimsParser = claimsParser ?? throw new ArgumentNullException(nameof(claimsParser));
    }

    [Function("CheckAdmin")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "auth/check-admin")] HttpRequest req)
    {
        try
        {
            // Extract authenticated user ID (Entra OID)
            var userId = req.GetAuthenticatedUserId(_claimsParser);

            _logger.LogInformation("Extracted userId: {UserId}", userId ?? "NULL");

            if (string.IsNullOrEmpty(userId))
            {
                return new CorsObjectResult(new { isAdmin = false, userId = (string?)null, error = "Not authenticated - no user ID found" })
                {
                    StatusCode = StatusCodes.Status401Unauthorized
                };
            }

            _logger.LogInformation("Checking admin status for user: {UserId}", userId);

            // Get roles for debugging
            var roles = _claimsParser.GetUserRoles(req);
            var hasAdminRole = req.IsAdmin(_claimsParser);
            var isAdminInOpenFGA = await _authService.IsAdmin(userId);

            // Check if user is admin via OpenFGA or app roles
            var isAdmin = hasAdminRole || isAdminInOpenFGA;

            return new CorsObjectResult(new
            {
                isAdmin = isAdmin,
                userId = userId,
                debug = new
                {
                    rolesFromToken = roles,
                    hasAdminRoleInToken = hasAdminRole,
                    isAdminInOpenFGA = isAdminInOpenFGA,
                    finalAdminStatus = isAdmin
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking admin status");
            return new CorsObjectResult(new
            {
                isAdmin = false,
                userId = (string?)null,
                error = ex.Message
            })
            {
                StatusCode = StatusCodes.Status500InternalServerError
            };
        }
    }
}
