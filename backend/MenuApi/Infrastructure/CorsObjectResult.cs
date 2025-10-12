using Microsoft.AspNetCore.Mvc;

namespace MenuApi.Infrastructure;

/// <summary>
/// Custom ObjectResult that adds CORS headers to responses
/// </summary>
public class CorsObjectResult : ObjectResult
{
    public CorsObjectResult(object? value) : base(value)
    {
    }

    public override void ExecuteResult(ActionContext context)
    {
        AddCorsHeaders(context);
        base.ExecuteResult(context);
    }

    public override Task ExecuteResultAsync(ActionContext context)
    {
        AddCorsHeaders(context);
        return base.ExecuteResultAsync(context);
    }

    private static void AddCorsHeaders(ActionContext context)
    {
        var request = context.HttpContext.Request;
        var headers = context.HttpContext.Response.Headers;

        // Get the origin from the request headers
        var origin = request.Headers.ContainsKey("Origin")
            ? request.Headers["Origin"].ToString()
            : null;

        // Allow specific origins for local development and production
        var allowedOrigins = new[]
        {
            "http://localhost:5173",  // Vite dev server
            "http://localhost:3000",  // Alternative dev port
            "https://witty-flower-068de881e.2.azurestaticapps.net"  // Production
        };

        // If origin is in allowed list, use it; otherwise use first allowed origin as fallback
        var allowOrigin = origin != null && allowedOrigins.Contains(origin)
            ? origin
            : allowedOrigins[0];

        if (!headers.ContainsKey("Access-Control-Allow-Origin"))
            headers["Access-Control-Allow-Origin"] = allowOrigin;

        if (!headers.ContainsKey("Access-Control-Allow-Credentials"))
            headers["Access-Control-Allow-Credentials"] = "true";  // Required for credentials: 'include'

        if (!headers.ContainsKey("Access-Control-Allow-Methods"))
            headers["Access-Control-Allow-Methods"] = "GET, POST, PUT, DELETE, OPTIONS";

        if (!headers.ContainsKey("Access-Control-Allow-Headers"))
            headers["Access-Control-Allow-Headers"] = "Content-Type, Authorization, X-Requested-With, X-MS-CLIENT-PRINCIPAL";

        if (!headers.ContainsKey("Access-Control-Max-Age"))
            headers["Access-Control-Max-Age"] = "86400";
    }
}
