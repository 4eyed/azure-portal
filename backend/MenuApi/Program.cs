using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
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

        // Configure JWT Bearer Authentication for local dev (with MSAL token from frontend)
        // In production, Azure Static Web Apps handles auth via X-MS-CLIENT-PRINCIPAL header
        var tenantId = context.Configuration["AZURE_TENANT_ID"];
        var clientId = context.Configuration["AZURE_CLIENT_ID"];

        if (!string.IsNullOrEmpty(tenantId) && !string.IsNullOrEmpty(clientId))
        {
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.Authority = $"https://login.microsoftonline.com/{tenantId}/v2.0";
                    options.Audience = clientId;
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        ValidIssuers = new[]
                        {
                            $"https://login.microsoftonline.com/{tenantId}/v2.0",
                            $"https://sts.windows.net/{tenantId}/"
                        },
                        RoleClaimType = "roles" // Map JWT roles claim to User.IsInRole()
                    };
                });

            services.AddAuthorization();
        }

        // Register application services
        services.AddApplicationServices(context.Configuration);
    })
    .Build();

host.Run();
