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
            AddCorsHeaders(response);
            context.GetInvocationResult().Value = response;
            return;
        }

        // Process the request
        await next(context);

        // Add CORS headers to response
        var result = context.GetInvocationResult();
        if (result.Value is HttpResponseData responseData)
        {
            AddCorsHeaders(responseData);
        }
    }

    private static void AddCorsHeaders(HttpResponseData response)
    {
        response.Headers.Add("Access-Control-Allow-Origin", "*");
        response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS");
        response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Authorization, X-Requested-With");
        response.Headers.Add("Access-Control-Max-Age", "86400");
    }
}
