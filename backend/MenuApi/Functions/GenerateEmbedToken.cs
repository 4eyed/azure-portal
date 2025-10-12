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
/// Function to generate Power BI embed token
/// </summary>
public class GenerateEmbedToken
{
    private readonly ILogger<GenerateEmbedToken> _logger;
    private readonly IPowerBIService _powerBIService;
    private readonly IClaimsPrincipalParser _claimsParser;

    public GenerateEmbedToken(
        ILogger<GenerateEmbedToken> logger,
        IPowerBIService powerBIService,
        IClaimsPrincipalParser claimsParser)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _powerBIService = powerBIService ?? throw new ArgumentNullException(nameof(powerBIService));
        _claimsParser = claimsParser ?? throw new ArgumentNullException(nameof(claimsParser));
    }

    [Function("GenerateEmbedToken")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "powerbi/embed-token")] HttpRequest req)
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

            var body = await new StreamReader(req.Body).ReadToEndAsync();
            var request = JsonSerializer.Deserialize<EmbedTokenRequest>(body, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (request == null || string.IsNullOrEmpty(request.WorkspaceId) || string.IsNullOrEmpty(request.ReportId))
            {
                return new BadRequestObjectResult(new ErrorResponse
                {
                    Error = "WorkspaceId and ReportId are required"
                });
            }

            _logger.LogInformation("Generating embed token for workspace: {WorkspaceId}, report: {ReportId}",
                request.WorkspaceId, request.ReportId);

            var result = await _powerBIService.GenerateEmbedToken(request.WorkspaceId, request.ReportId, userToken);

            return new OkObjectResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating embed token");
            return new ObjectResult(new ErrorResponse
            {
                Error = "Failed to generate embed token",
                Message = ex.Message
            })
            {
                StatusCode = StatusCodes.Status500InternalServerError
            };
        }
    }
}
