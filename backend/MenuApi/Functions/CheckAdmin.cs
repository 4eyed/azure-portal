using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using MenuApi.Services;
using MenuApi.Extensions;
using MenuApi.Infrastructure;

namespace MenuApi.Functions;

/// <summary>
/// Function to check if the authenticated user is an admin
/// </summary>
public class CheckAdmin
{
    private readonly ILogger<CheckAdmin> _logger;
    private readonly IAuthorizationService _authService;
    private readonly IClaimsPrincipalParser _claimsParser;
    private readonly IJwtTokenValidator _jwtValidator;

    public CheckAdmin(
        ILogger<CheckAdmin> logger,
        IAuthorizationService authService,
        IClaimsPrincipalParser claimsParser,
        IJwtTokenValidator jwtValidator)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        _claimsParser = claimsParser ?? throw new ArgumentNullException(nameof(claimsParser));
        _jwtValidator = jwtValidator ?? throw new ArgumentNullException(nameof(jwtValidator));
    }

    [Function("CheckAdmin")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "auth/check-admin")] HttpRequest req)
    {
        try
        {
            // Extract SQL token from request header and store in AsyncLocal context
            req.ExtractAndStoreSqlToken(_logger);

            // Manually validate JWT token and populate HttpContext.User
            var principal = await _jwtValidator.ValidateTokenAsync(req);
            if (principal != null && req.HttpContext != null)
            {
                req.HttpContext.User = principal;
                _logger.LogInformation("Token validated, user populated in HttpContext");
            }

            // Debug: Log authentication state
            _logger.LogInformation("CheckAdmin called - IsAuthenticated: {IsAuth}, HasUser: {HasUser}",
                req.HttpContext?.User?.Identity?.IsAuthenticated ?? false,
                req.HttpContext?.User != null);

            if (req.HttpContext?.User != null)
            {
                var claims = req.HttpContext.User.Claims.Select(c => $"{c.Type}={c.Value}").Take(10).ToList();
                _logger.LogInformation("Claims found: {Claims}", string.Join(", ", claims));
            }

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
