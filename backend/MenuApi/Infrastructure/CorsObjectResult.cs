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
        var headers = context.HttpContext.Response.Headers;

        if (!headers.ContainsKey("Access-Control-Allow-Origin"))
            headers.Add("Access-Control-Allow-Origin", "*");

        if (!headers.ContainsKey("Access-Control-Allow-Methods"))
            headers.Add("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS");

        if (!headers.ContainsKey("Access-Control-Allow-Headers"))
            headers.Add("Access-Control-Allow-Headers", "Content-Type, Authorization, X-Requested-With");

        if (!headers.ContainsKey("Access-Control-Max-Age"))
            headers.Add("Access-Control-Max-Age", "86400");
    }
}
