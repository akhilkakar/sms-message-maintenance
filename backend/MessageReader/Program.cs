using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Azure.Storage.Queues;
using System;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();
        
        // Register QueueClient for enqueuing messages
        services.AddSingleton(sp =>
        {
            var storageConnectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
            var queueName = Environment.GetEnvironmentVariable("QueueName") ?? "message-processing";
            
            var queueServiceClient = new QueueServiceClient(storageConnectionString);
            var queueClient = queueServiceClient.GetQueueClient(queueName);
            
            // Create queue if it doesn't exist
            queueClient.CreateIfNotExists();
            
            return queueClient;
        });
        
        // Optional: Add repository pattern for database access
        // services.AddScoped<IMessageRepository, MessageRepository>();
    })
    .ConfigureLogging(logging =>
    {
        logging.AddApplicationInsights();
        logging.SetMinimumLevel(LogLevel.Information);
        
        // Filter out verbose logs
        logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning);
        logging.AddFilter("Azure.Core", LogLevel.Warning);
    })
    .Build();

host.Run();