using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();
        
        // Register HttpClient for SMS API
        services.AddHttpClient("SmsApiClient", client =>
        {
            var apiUrl = Environment.GetEnvironmentVariable("ThirdPartyApiUrl") 
                ?? "https://api.sms-provider.com/send";
            client.BaseAddress = new Uri(apiUrl);
            client.Timeout = TimeSpan.FromSeconds(10);
        });
    })
    .ConfigureLogging(logging =>
    {
        logging.AddApplicationInsights();
        logging.SetMinimumLevel(LogLevel.Information);
    })
    .Build();

host.Run();