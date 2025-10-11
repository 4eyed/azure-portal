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
            var apiUrl = Environment.GetEnvironmentVariable("OPENFGA_API_URL") ?? "http://localhost:8080";
            var storeId = Environment.GetEnvironmentVariable("OPENFGA_STORE_ID") ?? "";

            Console.WriteLine("============================================");
            Console.WriteLine("OpenFGA Client Configuration");
            Console.WriteLine("============================================");
            Console.WriteLine($"API URL: {apiUrl}");
            Console.WriteLine($"Store ID: {(string.IsNullOrEmpty(storeId) ? "NOT SET!" : storeId)}");
            Console.WriteLine("============================================");

            if (string.IsNullOrEmpty(storeId))
            {
                Console.WriteLine("⚠️  WARNING: OPENFGA_STORE_ID is not set!");
                Console.WriteLine("   OpenFGA SDK may not work correctly.");
            }

            var configuration = new ClientConfiguration
            {
                ApiUrl = apiUrl,
                StoreId = storeId
            };
            return new OpenFgaClient(configuration);
        });
    })
    .Build();

host.Run();
