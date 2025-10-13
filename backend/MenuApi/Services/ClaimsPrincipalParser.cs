using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace MenuApi.Services;

/// <summary>
/// Parses the X-MS-CLIENT-PRINCIPAL header from Azure Static Web Apps
/// </summary>
public interface IClaimsPrincipalParser
{
    /// <summary>
    /// Extract user ID from the X-MS-CLIENT-PRINCIPAL header
    /// </summary>
    string? GetUserId(HttpRequest request);

    /// <summary>
    /// Extract user roles from the X-MS-CLIENT-PRINCIPAL header
    /// </summary>
    string[] GetUserRoles(HttpRequest request);

    /// <summary>
    /// Check if user has a specific role
    /// </summary>
    bool HasRole(HttpRequest request, string role);
}

public class ClaimsPrincipalParser : IClaimsPrincipalParser
{
    private const string ClientPrincipalHeader = "X-MS-CLIENT-PRINCIPAL";

    private const string AuthorizationHeader = "Authorization";
    private readonly ILogger<ClaimsPrincipalParser> _logger;

    public ClaimsPrincipalParser(ILogger<ClaimsPrincipalParser> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string? GetUserId(HttpRequest request)
    {
        var principal = EnsurePrincipal(request);

        // Priority 1: Check JWT claims (local dev with Authorization Bearer token)
        var oidClaim = principal?.FindFirst("oid")
            ?? principal?.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")
            ?? principal?.FindFirst(ClaimTypes.NameIdentifier);

        if (oidClaim != null && !string.IsNullOrEmpty(oidClaim.Value))
        {
            return oidClaim.Value;
        }

        // Priority 2: Check X-MS-CLIENT-PRINCIPAL header (Azure SWA production)
        var clientPrincipal = ParseClientPrincipal(request);

        // Use Entra Object ID (OID) from claims for stable user identity
        if (clientPrincipal?.Claims != null)
        {
            var oidClaim = clientPrincipal.Claims.FirstOrDefault(c =>
                c.Typ?.Equals("oid", StringComparison.OrdinalIgnoreCase) == true ||
                c.Typ?.Equals("http://schemas.microsoft.com/identity/claims/objectidentifier", StringComparison.OrdinalIgnoreCase) == true);

            if (!string.IsNullOrEmpty(oidClaim?.Val))
            {
                return oidClaim.Val;
            }
        }

        // Priority 3: Fallback to UserId field (email or username)
        if (!string.IsNullOrEmpty(clientPrincipal?.UserId))
        {
            return clientPrincipal.UserId;
        }

        // Priority 4: Fallback to UserDetails
        return clientPrincipal?.UserDetails;
    }

    public string[] GetUserRoles(HttpRequest request)
    {
        var principal = EnsurePrincipal(request);

        // Priority 1: Check JWT claims (local dev with Authorization Bearer token)
        if (principal?.Identity?.IsAuthenticated == true)
        {
            var roles = principal.Claims
                .Where(c => c.Type == "roles" || c.Type == ClaimTypes.Role)
                .Select(c => c.Value)
                .ToArray();

            if (roles.Length > 0)
            {
                return roles;
            }
        }

        // Priority 2: Check X-MS-CLIENT-PRINCIPAL header (Azure SWA production)
        var swaPrincipal = ParseClientPrincipal(request);
        return swaPrincipal?.UserRoles ?? Array.Empty<string>();
    }

    public bool HasRole(HttpRequest request, string role)
    {
        var principal = EnsurePrincipal(request);

        // Priority 1: Check JWT claims using standard ASP.NET Core method
        if (principal?.Identity?.IsAuthenticated == true && principal.IsInRole(role))
        {
            return true;
        }

        // Priority 2: Check X-MS-CLIENT-PRINCIPAL header
        var roles = GetUserRoles(request);
        return roles.Contains(role, StringComparer.OrdinalIgnoreCase);
    }

    private ClaimsPrincipal? EnsurePrincipal(HttpRequest request)
    {
        var principal = request.HttpContext?.User;
        if (principal?.Identity?.IsAuthenticated == true)
        {
            return principal;
        }

        var bearerPrincipal = ParseBearerPrincipal(request);
        if (bearerPrincipal != null)
        {
            if (request.HttpContext is not null)
            {
                request.HttpContext.User = bearerPrincipal;
            }

            return bearerPrincipal;
        }

        return principal;
    }

    private ClaimsPrincipal? ParseBearerPrincipal(HttpRequest request)
    {
        if (!request.Headers.TryGetValue(AuthorizationHeader, out var headerValues))
        {
            return null;
        }

        var header = headerValues.ToString();
        if (string.IsNullOrWhiteSpace(header) || !header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var token = header[7..].Trim();
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        try
        {
            var handler = new JwtSecurityTokenHandler();
            var jwt = handler.ReadJwtToken(token);
            var baseClaims = jwt.Claims.ToList();
            var identity = new ClaimsIdentity(baseClaims, "Bearer", ClaimTypes.NameIdentifier, ClaimTypes.Role);

            foreach (var roleClaim in baseClaims.Where(c => string.Equals(c.Type, "roles", StringComparison.OrdinalIgnoreCase)))
            {
                identity.AddClaim(new Claim(ClaimTypes.Role, roleClaim.Value));
            }
            var principal = new ClaimsPrincipal(identity);

            _logger.LogDebug(
                "Constructed claims principal from Authorization header with audience {Audience} and subject {Subject}.", 
                jwt.Audiences.FirstOrDefault() ?? "<unknown>",
                jwt.Subject ?? "<unknown>");

            if (jwt.ValidTo != DateTime.MinValue)
            {
                _logger.LogDebug("Bearer token expires at {Expiry:u} (UTC).", jwt.ValidTo);
            }

            return principal;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse Authorization bearer token. Falling back to Static Web Apps headers.");
            return null;
        }
    }

    private ClientPrincipal? ParseClientPrincipal(HttpRequest request)
    {
        if (!request.Headers.TryGetValue(ClientPrincipalHeader, out var headerValue))
        {
            return null;
        }

        var headerString = headerValue.ToString();
        if (string.IsNullOrEmpty(headerString))
        {
            return null;
        }

        try
        {
            var data = Convert.FromBase64String(headerString);
            var json = Encoding.UTF8.GetString(data);
            return JsonSerializer.Deserialize<ClientPrincipal>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (Exception)
        {
            // If parsing fails, return null (user is unauthenticated)
            return null;
        }
    }
}

/// <summary>
/// Represents the client principal data from Azure Static Web Apps
/// </summary>
public class ClientPrincipal
{
    public string? IdentityProvider { get; set; }
    public string? UserId { get; set; }
    public string? UserDetails { get; set; }
    public string[]? UserRoles { get; set; }
    public IEnumerable<ClientPrincipalClaim>? Claims { get; set; }
}

public class ClientPrincipalClaim
{
    public string? Typ { get; set; }
    public string? Val { get; set; }
}
