using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MenuApi.Data;
using OpenFga.Sdk.Client;
using OpenFga.Sdk.Configuration;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services =>
    {
        // Register Entity Framework DbContext
        var connectionString = Environment.GetEnvironmentVariable("DOTNET_CONNECTION_STRING");
        if (!string.IsNullOrEmpty(connectionString))
        {
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(connectionString));
        }

        // Register OpenFGA client
        services.AddSingleton<OpenFgaClient>(sp =>
        {
            var configuration = new ClientConfiguration
            {
                ApiUrl = Environment.GetEnvironmentVariable("OPENFGA_API_URL") ?? "http://localhost:8080",
                StoreId = Environment.GetEnvironmentVariable("OPENFGA_STORE_ID") ?? ""
            };
            return new OpenFgaClient(configuration);
        });
    })
    .Build();

host.Run();
