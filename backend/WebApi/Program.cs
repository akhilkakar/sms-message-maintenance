using System;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SmsMessageMaintenanceData;
using SmsMessageMaintenanceSecurity;
using SmsMessageMaintenanceServices;
using SmsMessageMaintenanceValidation;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices(services =>
    {
        // Telemetry and monitoring
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();
        
        // HTTP client for external APIs
        services.AddHttpClient();

        // Register dependencies following Dependency Inversion Principle
        // All classes depend on interfaces, making them easily testable and replaceable
        
        // Validation layer
        services.AddScoped<IMessageValidator, MessageValidator>();
        
        // Security layer
        services.AddScoped<IInputSanitizer, InputSanitizer>();
        
        // Data access layer
        services.AddScoped<IMessageRepository>(provider => 
        {
            var logger = provider.GetRequiredService<ILogger<MessageRepository>>();
            var connectionString = Environment.GetEnvironmentVariable("SqlConnectionString");
            
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException(
                    "SqlConnectionString environment variable is not configured");
            }
            
            return new MessageRepository(connectionString, logger);
        });
        
        // Business logic layer
        services.AddScoped<IMessageService, MessageService>();
        
        // Note: Functions are automatically registered by the Functions runtime
        // CreateMessageFunction will receive injected dependencies through constructor
    })
    .ConfigureLogging(logging =>
    {
        logging.AddApplicationInsights();
        logging.SetMinimumLevel(LogLevel.Information);
        
        // Filter out verbose logs
        logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);
        logging.AddFilter("System.Net.Http", LogLevel.Warning);
    })
    .Build();

host.Run();