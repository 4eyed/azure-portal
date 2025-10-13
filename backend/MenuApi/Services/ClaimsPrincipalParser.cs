using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;

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
    private const string AuthTokenHeader = "X-MS-AUTH-TOKEN";

    public string? GetUserId(HttpRequest request)
    {
        // Priority 1: Check X-MS-AUTH-TOKEN JWT (Azure SWA linked backend)
        var jwtClaims = ParseJwtToken(request);
        if (jwtClaims != null)
        {
            var oidClaim = jwtClaims.FirstOrDefault(c =>
                c.Type == "oid" ||
                c.Type == "http://schemas.microsoft.com/identity/claims/objectidentifier");

            if (oidClaim != null && !string.IsNullOrEmpty(oidClaim.Value))
            {
                return oidClaim.Value;
            }

            // Fallback to sub claim
            var subClaim = jwtClaims.FirstOrDefault(c => c.Type == "sub");
            if (subClaim != null && !string.IsNullOrEmpty(subClaim.Value))
            {
                return subClaim.Value;
            }
        }

        // Priority 2: Check JWT claims (local dev with Authorization Bearer token)
        if (request.HttpContext?.User?.Identity?.IsAuthenticated == true)
        {
            var oidClaim = request.HttpContext.User.FindFirst("oid")
                ?? request.HttpContext.User.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")
                ?? request.HttpContext.User.FindFirst(ClaimTypes.NameIdentifier);

            if (oidClaim != null && !string.IsNullOrEmpty(oidClaim.Value))
            {
                return oidClaim.Value;
            }
        }

        // Priority 3: Check X-MS-CLIENT-PRINCIPAL header (Azure SWA production - if available)
        var principal = ParseClientPrincipal(request);

        // Use Entra Object ID (OID) from claims for stable user identity
        if (principal?.Claims != null)
        {
            var oidClaim = principal.Claims.FirstOrDefault(c =>
                c.Typ?.Equals("oid", StringComparison.OrdinalIgnoreCase) == true ||
                c.Typ?.Equals("http://schemas.microsoft.com/identity/claims/objectidentifier", StringComparison.OrdinalIgnoreCase) == true);

            if (!string.IsNullOrEmpty(oidClaim?.Val))
            {
                return oidClaim.Val;
            }
        }

        // Priority 3: Fallback to UserId field (email or username)
        if (!string.IsNullOrEmpty(principal?.UserId))
        {
            return principal.UserId;
        }

        // Priority 4: Fallback to UserDetails
        return principal?.UserDetails;
    }

    public string[] GetUserRoles(HttpRequest request)
    {
        // Priority 1: Check X-MS-AUTH-TOKEN JWT (Azure SWA linked backend)
        var jwtClaims = ParseJwtToken(request);
        if (jwtClaims != null)
        {
            var roles = jwtClaims
                .Where(c => c.Type == "roles" || c.Type == ClaimTypes.Role)
                .Select(c => c.Value)
                .ToArray();

            if (roles.Length > 0)
            {
                return roles;
            }
        }

        // Priority 2: Check JWT claims (local dev with Authorization Bearer token)
        if (request.HttpContext?.User?.Identity?.IsAuthenticated == true)
        {
            var roles = request.HttpContext.User.Claims
                .Where(c => c.Type == "roles" || c.Type == ClaimTypes.Role)
                .Select(c => c.Value)
                .ToArray();

            if (roles.Length > 0)
            {
                return roles;
            }
        }

        // Priority 3: Check X-MS-CLIENT-PRINCIPAL header (Azure SWA production - if available)
        var principal = ParseClientPrincipal(request);
        return principal?.UserRoles ?? Array.Empty<string>();
    }

    public bool HasRole(HttpRequest request, string role)
    {
        // Priority 1: Check JWT claims using standard ASP.NET Core method
        if (request.HttpContext?.User?.Identity?.IsAuthenticated == true)
        {
            if (request.HttpContext.User.IsInRole(role))
            {
                return true;
            }
        }

        // Priority 2: Check X-MS-CLIENT-PRINCIPAL header
        var roles = GetUserRoles(request);
        return roles.Contains(role, StringComparer.OrdinalIgnoreCase);
    }

    private List<Claim>? ParseJwtToken(HttpRequest request)
    {
        // Check for X-MS-AUTH-TOKEN header (Azure SWA linked backend)
        if (!request.Headers.TryGetValue(AuthTokenHeader, out var authHeaderValue))
        {
            return null;
        }

        var authHeader = authHeaderValue.ToString();
        if (string.IsNullOrEmpty(authHeader))
        {
            return null;
        }

        try
        {
            // Remove "Bearer " prefix if present
            var token = authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                ? authHeader.Substring(7)
                : authHeader;

            // JWT format: header.payload.signature
            var parts = token.Split('.');
            if (parts.Length != 3)
            {
                return null;
            }

            // Decode the payload (second part)
            var payload = parts[1];

            // Add padding if needed for base64 decoding
            switch (payload.Length % 4)
            {
                case 2: payload += "=="; break;
                case 3: payload += "="; break;
            }

            var jsonBytes = Convert.FromBase64String(payload);
            var json = Encoding.UTF8.GetString(jsonBytes);

            // Parse JSON to get claims
            var claimsDoc = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
            if (claimsDoc == null)
            {
                return null;
            }

            var claims = new List<Claim>();
            foreach (var kvp in claimsDoc)
            {
                // Handle array values (like roles)
                if (kvp.Value.ValueKind == JsonValueKind.Array)
                {
                    foreach (var element in kvp.Value.EnumerateArray())
                    {
                        if (element.ValueKind == JsonValueKind.String)
                        {
                            claims.Add(new Claim(kvp.Key, element.GetString() ?? ""));
                        }
                    }
                }
                // Handle string values
                else if (kvp.Value.ValueKind == JsonValueKind.String)
                {
                    claims.Add(new Claim(kvp.Key, kvp.Value.GetString() ?? ""));
                }
                // Handle number values
                else if (kvp.Value.ValueKind == JsonValueKind.Number)
                {
                    claims.Add(new Claim(kvp.Key, kvp.Value.ToString()));
                }
                // Handle boolean values
                else if (kvp.Value.ValueKind == JsonValueKind.True || kvp.Value.ValueKind == JsonValueKind.False)
                {
                    claims.Add(new Claim(kvp.Key, kvp.Value.GetBoolean().ToString()));
                }
            }

            return claims;
        }
        catch (Exception)
        {
            // If JWT parsing fails, return null (user is unauthenticated)
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
