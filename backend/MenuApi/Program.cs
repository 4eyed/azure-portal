using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MenuApi.Configuration;
using MenuApi.Middleware;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication(builder =>
    {
        // CORS is now handled by CorsObjectResult in the functions
        // builder.UseMiddleware<CorsMiddleware>();
    })
    .ConfigureServices((context, services) =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        // Enable HttpContext access in Azure Functions Isolated Worker
        services.AddHttpContextAccessor();

        // Register application services
        services.AddApplicationServices(context.Configuration);
    })
    .Build();

host.Run();
