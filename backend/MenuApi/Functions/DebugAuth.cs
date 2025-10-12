using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using MenuApi.Services;
using MenuApi.Infrastructure;

namespace MenuApi.Functions;

/// <summary>
/// Debug endpoint to inspect authentication details
/// </summary>
public class DebugAuth
{
    private readonly ILogger<DebugAuth> _logger;
    private readonly IClaimsPrincipalParser _claimsParser;

    public DebugAuth(ILogger<DebugAuth> logger, IClaimsPrincipalParser claimsParser)
    {
        _logger = logger;
        _claimsParser = claimsParser;
    }

    [Function("DebugAuth")]
    public IActionResult Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "debug/auth")] HttpRequest req)
    {
        var userId = _claimsParser.GetUserId(req);
        var roles = _claimsParser.GetUserRoles(req);

        // Check for X-MS-CLIENT-PRINCIPAL header
        var hasHeader = req.Headers.TryGetValue("X-MS-CLIENT-PRINCIPAL", out var headerValue);

        var response = new
        {
            userId = userId,
            roles = roles,
            hasClientPrincipalHeader = hasHeader,
            headerPreview = hasHeader ? headerValue.ToString().Substring(0, Math.Min(50, headerValue.ToString().Length)) + "..." : null,
            allHeaders = req.Headers.Keys.ToList(),
            queryParams = req.Query.ToDictionary(k => k.Key, k => k.Value.ToString())
        };

        _logger.LogInformation("Debug auth info: {@Response}", response);

        return new CorsObjectResult(response);
    }
}
