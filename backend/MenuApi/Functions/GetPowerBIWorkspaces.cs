using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using MenuApi.Models.DTOs;
using MenuApi.Services;
using MenuApi.Extensions;

namespace MenuApi.Functions;

/// <summary>
/// Function to retrieve Power BI workspaces
/// </summary>
public class GetPowerBIWorkspaces
{
    private readonly ILogger<GetPowerBIWorkspaces> _logger;
    private readonly IPowerBIService _powerBIService;
    private readonly IClaimsPrincipalParser _claimsParser;

    public GetPowerBIWorkspaces(
        ILogger<GetPowerBIWorkspaces> logger,
        IPowerBIService powerBIService,
        IClaimsPrincipalParser claimsParser)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _powerBIService = powerBIService ?? throw new ArgumentNullException(nameof(powerBIService));
        _claimsParser = claimsParser ?? throw new ArgumentNullException(nameof(claimsParser));
    }

    [Function("GetPowerBIWorkspaces")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "powerbi/workspaces")] HttpRequest req)
    {
        try
        {
            // Extract authenticated user ID
            var userId = req.GetAuthenticatedUserId(_claimsParser);
            if (string.IsNullOrEmpty(userId))
            {
                return new UnauthorizedObjectResult(new ErrorResponse
                {
                    Error = "User is not authenticated"
                });
            }

            _logger.LogInformation("Fetching Power BI workspaces");

            // Extract the user's access token from the Authorization header
            var authHeader = req.Headers["Authorization"].ToString();
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
            {
                return new UnauthorizedObjectResult(new ErrorResponse
                {
                    Error = "Authorization header is required"
                });
            }

            var userToken = authHeader.Substring("Bearer ".Length).Trim();

            // Log token info for debugging (first 20 chars only)
            _logger.LogInformation("Token received, length: {TokenLength}, preview: {TokenPreview}...",
                userToken.Length,
                userToken.Substring(0, Math.Min(20, userToken.Length)));

            // Try to decode and log audience/scope
            try
            {
                var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
                var token = handler.ReadJwtToken(userToken);
                _logger.LogInformation("Token audience: {Audience}", token.Audiences.FirstOrDefault());
                _logger.LogInformation("Token scopes: {Scopes}", token.Claims.FirstOrDefault(c => c.Type == "scp")?.Value);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not decode token");
            }

            var workspaces = await _powerBIService.GetWorkspaces(userToken);

            return new OkObjectResult(workspaces);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching Power BI workspaces");
            return new ObjectResult(new ErrorResponse
            {
                Error = "Failed to fetch Power BI workspaces",
                Message = ex.Message
            })
            {
                StatusCode = StatusCodes.Status500InternalServerError
            };
        }
    }
}
