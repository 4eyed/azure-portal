using System;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using MenuApi.Infrastructure;
using MenuApi.Services;

namespace MenuApi.Extensions;

/// <summary>
/// Extension methods for HttpRequest to simplify authentication
/// </summary>
public static class HttpRequestExtensions
{
    private const string SqlTokenHeaderName = "X-SQL-Token";

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

    /// <summary>
    /// Extracts the delegated SQL access token from the request and stores it in an AsyncLocal scope.
    /// Dispose the returned scope when the request handling is finished to clear the token.
    /// </summary>
    public static IDisposable BeginSqlTokenScope(this HttpRequest req, ILogger logger)
    {
        string? token = null;

        if (req.Headers.TryGetValue(SqlTokenHeaderName, out var headerValue))
        {
            token = headerValue.ToString();

            if (!string.IsNullOrWhiteSpace(token))
            {
                logger.LogInformation(
                    "Received delegated SQL token from header {Header} (length: {Length}, preview: {Preview})",
                    SqlTokenHeaderName,
                    token.Length,
                    GetPreview(token));
            }
            else
            {
                logger.LogWarning(
                    "Header {Header} was present but empty; falling back to configured authentication.",
                    SqlTokenHeaderName);
                token = null;
            }
        }
        else
        {
            logger.LogWarning(
                "Header {Header} not found; database calls will use managed identity or connection string credentials.",
                SqlTokenHeaderName);
        }

        return SqlTokenContext.BeginScope(token);
    }

    private static string GetPreview(string token)
    {
        const int previewLength = 12;
        return token.Length <= previewLength
            ? token
            : $"{token[..previewLength]}…";
    }
}
