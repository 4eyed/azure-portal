using System;
using Microsoft.AspNetCore.Http;
using MenuApi.Services;

namespace MenuApi.Extensions;

/// <summary>
/// Extension methods for HttpRequest to simplify authentication
/// </summary>
public static class HttpRequestExtensions
{
    /// <summary>
    /// Gets the authenticated user ID from the request, with fallback to query param for local dev
    /// </summary>
    public static string? GetAuthenticatedUserId(this HttpRequest req, IClaimsPrincipalParser claimsParser)
    {
        // Try to get from X-MS-CLIENT-PRINCIPAL header (production)
        var userId = claimsParser.GetUserId(req);

        // Fallback to query parameter for local development
        if (string.IsNullOrEmpty(userId))
        {
            userId = req.Query["user"].ToString();
        }

        return string.IsNullOrEmpty(userId) ? null : userId;
    }

    /// <summary>
    /// Checks if the authenticated user has the specified role
    /// </summary>
    public static bool HasRole(this HttpRequest req, IClaimsPrincipalParser claimsParser, string role)
    {
        return claimsParser.HasRole(req, role);
    }

    /// <summary>
    /// Checks if the user is an admin (has admin role or uses admin query param in local dev)
    /// </summary>
    public static bool IsAdmin(this HttpRequest req, IClaimsPrincipalParser claimsParser)
    {
        // Check X-MS-CLIENT-PRINCIPAL header for admin role
        if (claimsParser.HasRole(req, "admin"))
        {
            return true;
        }

        // Fallback to query parameter for local development
        var isAdminQuery = req.Query["isAdmin"].ToString();
        return !string.IsNullOrEmpty(isAdminQuery) &&
               (isAdminQuery.Equals("true", StringComparison.OrdinalIgnoreCase) || isAdminQuery == "1");
    }

}
