using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker.Middleware;
using System.Net;

namespace MenuApi.Middleware;

/// <summary>
/// CORS middleware for Azure Functions Isolated Worker
/// </summary>
public class CorsMiddleware : IFunctionsWorkerMiddleware
{
    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        var requestData = await context.GetHttpRequestDataAsync();

        // Handle preflight OPTIONS requests
        if (requestData != null && requestData.Method.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase))
        {
            var response = requestData.CreateResponse(HttpStatusCode.OK);
            AddCorsHeaders(requestData, response);
            context.GetInvocationResult().Value = response;
            return;
        }

        // Process the request
        await next(context);

        // Add CORS headers to response
        var result = context.GetInvocationResult();
        if (result.Value is HttpResponseData responseData && requestData != null)
        {
            AddCorsHeaders(requestData, responseData);
        }
    }

    private static void AddCorsHeaders(HttpRequestData request, HttpResponseData response)
    {
        // Get the origin from the request headers
        var origin = request.Headers.TryGetValues("Origin", out var origins)
            ? origins.FirstOrDefault()
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

        response.Headers.Add("Access-Control-Allow-Origin", allowOrigin);
        response.Headers.Add("Access-Control-Allow-Credentials", "true");  // Required for credentials: 'include'
        response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS");
        response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Authorization, X-Requested-With, X-MS-CLIENT-PRINCIPAL");
        response.Headers.Add("Access-Control-Max-Age", "86400");
    }
}
