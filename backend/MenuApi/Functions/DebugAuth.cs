using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using MenuApi.Services;
using MenuApi.Infrastructure;

namespace MenuApi.Functions;

/// <summary>
/// Comprehensive authentication diagnostics endpoint
/// Inspects all authentication-related headers and context
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
        var report = new StringBuilder();
        report.AppendLine("========================================");
        report.AppendLine("Authentication Diagnostics");
        report.AppendLine($"Timestamp: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        report.AppendLine("========================================");
        report.AppendLine();

        // Section 1: User Identity from ClaimsPrincipalParser
        AppendUserIdentitySection(report);

        // Section 2: All Authentication Headers
        AppendAuthenticationHeadersSection(report, req);

        // Section 3: All Request Headers (for debugging)
        AppendAllHeadersSection(report, req);

        // Section 4: Query Parameters
        AppendQueryParametersSection(report, req);

        // Section 5: HttpContext User Claims (if available)
        AppendHttpContextUserSection(report, req);

        report.AppendLine("========================================");
        report.AppendLine("End of Authentication Diagnostics");
        report.AppendLine("========================================");

        var reportText = report.ToString();
        _logger.LogInformation("Authentication diagnostics:\n{Report}", reportText);

        return new CorsObjectResult(new
        {
            success = true,
            timestamp = DateTime.UtcNow,
            report = reportText,
            userId = _claimsParser.GetUserId(req),
            roles = _claimsParser.GetUserRoles(req)
        });
    }

    private void AppendUserIdentitySection(StringBuilder report)
    {
        report.AppendLine("USER IDENTITY (from ClaimsPrincipalParser)");
        report.AppendLine("----------------------------------------");
        report.AppendLine("(Parsed from available headers and claims)");
        report.AppendLine();
    }

    private void AppendAuthenticationHeadersSection(StringBuilder report, HttpRequest req)
    {
        report.AppendLine("AUTHENTICATION HEADERS");
        report.AppendLine("----------------------------------------");

        // X-MS-CLIENT-PRINCIPAL (Azure Static Web Apps)
        if (req.Headers.TryGetValue("X-MS-CLIENT-PRINCIPAL", out var clientPrincipal))
        {
            report.AppendLine("✅ X-MS-CLIENT-PRINCIPAL: Present");
            report.AppendLine($"   Length: {clientPrincipal.ToString().Length} characters");
            report.AppendLine($"   Preview: {CreatePreview(clientPrincipal.ToString(), 50)}");

            // Try to decode and show structure
            try
            {
                var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(clientPrincipal.ToString()));
                report.AppendLine($"   Decoded preview: {CreatePreview(decoded, 100)}");
            }
            catch
            {
                report.AppendLine("   (Unable to decode as base64)");
            }
        }
        else
        {
            report.AppendLine("❌ X-MS-CLIENT-PRINCIPAL: Missing");
        }

        // X-MS-AUTH-TOKEN (Azure Static Web Apps linked backend)
        if (req.Headers.TryGetValue("X-MS-AUTH-TOKEN", out var authToken))
        {
            report.AppendLine("✅ X-MS-AUTH-TOKEN: Present");
            report.AppendLine($"   Length: {authToken.ToString().Length} characters");
            report.AppendLine($"   Preview: {CreatePreview(authToken.ToString(), 50)}");
        }
        else
        {
            report.AppendLine("❌ X-MS-AUTH-TOKEN: Missing");
        }

        // X-MS-TOKEN-AAD-ID-TOKEN (Azure Static Web Apps with AAD)
        if (req.Headers.TryGetValue("X-MS-TOKEN-AAD-ID-TOKEN", out var aadIdToken))
        {
            report.AppendLine("✅ X-MS-TOKEN-AAD-ID-TOKEN: Present");
            report.AppendLine($"   Length: {aadIdToken.ToString().Length} characters");
            report.AppendLine($"   Preview: {CreatePreview(aadIdToken.ToString(), 50)}");
        }
        else
        {
            report.AppendLine("❌ X-MS-TOKEN-AAD-ID-TOKEN: Missing");
        }

        // X-MS-TOKEN-AAD-ACCESS-TOKEN
        if (req.Headers.TryGetValue("X-MS-TOKEN-AAD-ACCESS-TOKEN", out var aadAccessToken))
        {
            report.AppendLine("✅ X-MS-TOKEN-AAD-ACCESS-TOKEN: Present");
            report.AppendLine($"   Length: {aadAccessToken.ToString().Length} characters");
            report.AppendLine($"   Preview: {CreatePreview(aadAccessToken.ToString(), 50)}");
        }
        else
        {
            report.AppendLine("❌ X-MS-TOKEN-AAD-ACCESS-TOKEN: Missing");
        }

        // Authorization Header (Bearer token)
        if (req.Headers.TryGetValue("Authorization", out var authHeader))
        {
            report.AppendLine("✅ Authorization: Present");
            report.AppendLine($"   Value: {CreatePreview(authHeader.ToString(), 50)}");
        }
        else
        {
            report.AppendLine("❌ Authorization: Missing");
        }

        report.AppendLine();
    }

    private void AppendAllHeadersSection(StringBuilder report, HttpRequest req)
    {
        report.AppendLine("ALL REQUEST HEADERS");
        report.AppendLine("----------------------------------------");

        var headers = req.Headers
            .OrderBy(h => h.Key)
            .Select(h => $"{h.Key}: {(ShouldRedactHeader(h.Key) ? "[REDACTED]" : CreatePreview(h.Value.ToString(), 100))}")
            .ToList();

        if (headers.Any())
        {
            foreach (var header in headers)
            {
                report.AppendLine($"  {header}");
            }
        }
        else
        {
            report.AppendLine("  (No headers found)");
        }

        report.AppendLine();
    }

    private void AppendQueryParametersSection(StringBuilder report, HttpRequest req)
    {
        report.AppendLine("QUERY PARAMETERS");
        report.AppendLine("----------------------------------------");

        if (req.Query.Any())
        {
            foreach (var param in req.Query)
            {
                report.AppendLine($"  {param.Key} = {param.Value}");
            }
        }
        else
        {
            report.AppendLine("  (No query parameters)");
        }

        report.AppendLine();
    }

    private void AppendHttpContextUserSection(StringBuilder report, HttpRequest req)
    {
        report.AppendLine("HTTPCONTEXT USER CLAIMS");
        report.AppendLine("----------------------------------------");

        if (req.HttpContext?.User?.Identity?.IsAuthenticated == true)
        {
            report.AppendLine($"✅ User is authenticated");
            report.AppendLine($"   Authentication Type: {req.HttpContext.User.Identity.AuthenticationType}");
            report.AppendLine($"   Name: {req.HttpContext.User.Identity.Name}");
            report.AppendLine();
            report.AppendLine("   Claims:");

            foreach (var claim in req.HttpContext.User.Claims.OrderBy(c => c.Type))
            {
                var value = ShouldRedactClaimType(claim.Type)
                    ? "[REDACTED]"
                    : CreatePreview(claim.Value, 50);
                report.AppendLine($"     {claim.Type} = {value}");
            }
        }
        else
        {
            report.AppendLine("❌ No authenticated user in HttpContext");
        }

        report.AppendLine();
    }

    private static string CreatePreview(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
            return "[empty]";

        return value.Length <= maxLength
            ? value
            : $"{value[..maxLength]}…";
    }

    private static bool ShouldRedactHeader(string headerName)
    {
        var redactHeaders = new[] { "Authorization", "Cookie", "X-SQL-Token" };
        return redactHeaders.Any(h => h.Equals(headerName, StringComparison.OrdinalIgnoreCase));
    }

    private static bool ShouldRedactClaimType(string claimType)
    {
        var redactTypes = new[] { "pwd", "password", "secret" };
        return redactTypes.Any(t => claimType.Contains(t, StringComparison.OrdinalIgnoreCase));
    }
}
