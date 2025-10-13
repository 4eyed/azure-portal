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
            using var sqlTokenScope = req.BeginSqlTokenScope(_logger);

            // Extract authenticated user ID (Entra OID)
            var userId = req.GetAuthenticatedUserId(_claimsParser);

            _logger.LogDebug(
                "Auth header snapshot - X-MS-CLIENT-PRINCIPAL present: {HasSwaPrincipal}, Authorization present: {HasAuthorization}",
                req.Headers.ContainsKey("X-MS-CLIENT-PRINCIPAL"),
                req.Headers.ContainsKey("Authorization"));

            _logger.LogInformation("Extracted userId: {UserId}", userId ?? "NULL");

            if (string.IsNullOrEmpty(userId))
            {
                return new CorsObjectResult(new { isAdmin = false, userId = (string?)null, error = "Not authenticated - no user ID found" })
                {
                    StatusCode = StatusCodes.Status401Unauthorized
                };
            }

            _logger.LogInformation("Checking admin status for user: {UserId}", userId);

            // Check if user is admin via OpenFGA or app roles
            var isAdmin = req.IsAdmin(_claimsParser) || await _authService.IsAdmin(userId);

            return new CorsObjectResult(new
            {
                isAdmin = isAdmin,
                userId = userId
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
