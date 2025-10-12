using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using MenuApi.Models.DTOs;
using MenuApi.Services;
using MenuApi.Extensions;

namespace MenuApi.Functions;

/// <summary>
/// Function to retrieve Power BI reports in a workspace
/// </summary>
public class GetPowerBIReports
{
    private readonly ILogger<GetPowerBIReports> _logger;
    private readonly IPowerBIService _powerBIService;
    private readonly IClaimsPrincipalParser _claimsParser;

    public GetPowerBIReports(
        ILogger<GetPowerBIReports> logger,
        IPowerBIService powerBIService,
        IClaimsPrincipalParser claimsParser)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _powerBIService = powerBIService ?? throw new ArgumentNullException(nameof(powerBIService));
        _claimsParser = claimsParser ?? throw new ArgumentNullException(nameof(claimsParser));
    }

    [Function("GetPowerBIReports")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "powerbi/reports")] HttpRequest req)
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

            var workspaceId = req.Query["workspaceId"].ToString();
            if (string.IsNullOrEmpty(workspaceId))
            {
                return new BadRequestObjectResult(new ErrorResponse
                {
                    Error = "workspaceId query parameter is required"
                });
            }

            // Extract the user's access token from the Authorization header
            var authHeader = req.Headers["Authorization"].ToString();
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
            {
                return new UnauthorizedObjectResult(new ErrorResponse
                {
                    Error = "Authorization header is required"
                });
            }

            _logger.LogInformation("Fetching Power BI reports for workspace: {WorkspaceId}", workspaceId);

            var userToken = authHeader.Substring("Bearer ".Length).Trim();
            var reports = await _powerBIService.GetReports(workspaceId, userToken);

            return new OkObjectResult(reports);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching Power BI reports");
            return new ObjectResult(new ErrorResponse
            {
                Error = "Failed to fetch Power BI reports",
                Message = ex.Message
            })
            {
                StatusCode = StatusCodes.Status500InternalServerError
            };
        }
    }
}
