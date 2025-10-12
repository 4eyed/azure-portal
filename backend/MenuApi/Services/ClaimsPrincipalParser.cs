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

    public string? GetUserId(HttpRequest request)
    {
        var principal = ParseClientPrincipal(request);
        return principal?.UserId;
    }

    public string[] GetUserRoles(HttpRequest request)
    {
        var principal = ParseClientPrincipal(request);
        return principal?.UserRoles ?? Array.Empty<string>();
    }

    public bool HasRole(HttpRequest request, string role)
    {
        var roles = GetUserRoles(request);
        return roles.Contains(role, StringComparer.OrdinalIgnoreCase);
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
