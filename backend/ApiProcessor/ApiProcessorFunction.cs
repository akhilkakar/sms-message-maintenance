using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Data.SqlClient;

namespace SmsMessageMaintenanceFunctions
{
    public class ApiProcessorFunction
    {
        private readonly ILogger<ApiProcessorFunction> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly string _connectionString;

        // ✅ Use Dependency Injection in Isolated model
        public ApiProcessorFunction(
            ILogger<ApiProcessorFunction> logger,
            IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _connectionString = Environment.GetEnvironmentVariable("SqlConnectionString");
        }

        [Function("ApiProcessor")]
        public async Task Run(
            [QueueTrigger("message-processing", Connection = "AzureWebJobsStorage")] 
            string queueMessage)
        {
            _logger.LogInformation($"ApiProcessor processing message: {queueMessage}");

            try
            {
                var message = JsonSerializer.Deserialize<QueueMessageDto>(queueMessage);

                // Update status to Processing
                await UpdateMessageStatus(message.MessageId, "Processing", null);

                // Call third-party API
                var apiResponse = await CallThirdPartyApi(message);

                // Update final status based on API response
                await UpdateMessageStatus(message.MessageId, apiResponse.Status, apiResponse.Reason);

                _logger.LogInformation($"Message {message.MessageId} processed with status: {apiResponse.Status}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error processing message: {ex.Message}");
                throw; // Let message return to queue for retry
            }
        }

        private async Task<ApiResponse> CallThirdPartyApi(QueueMessageDto message)
        {
            try
            {
                // Simulate API call
                await Task.Delay(3000);
                var response = SimulateApiResponse(message);
                return response;

                /* Real implementation:
                var httpClient = _httpClientFactory.CreateClient("SmsApiClient");
                
                var request = new
                {
                    to = message.To.ToString(),
                    from = message.From.ToString(),
                    message = message.Message
                };

                var content = new StringContent(
                    JsonSerializer.Serialize(request),
                    Encoding.UTF8,
                    "application/json"
                );

                var httpResponse = await httpClient.PostAsync("", content);
                var responseBody = await httpResponse.Content.ReadAsStringAsync();
                var apiResult = JsonSerializer.Deserialize<ThirdPartyApiResponse>(responseBody);

                return new ApiResponse
                {
                    Status = apiResult.Status,
                    Reason = apiResult.Reason
                };
                */
            }
            catch (HttpRequestException ex)
            {
                return new ApiResponse
                {
                    Status = "Failed - API Error",
                    Reason = $"HTTP request failed: {ex.Message}"
                };
            }
            catch (TaskCanceledException)
            {
                return new ApiResponse
                {
                    Status = "Failed - API Error",
                    Reason = "API request timed out"
                };
            }
        }

        private ApiResponse SimulateApiResponse(QueueMessageDto message)
        {
            if (message.To.ToString().Length < 10)
            {
                return new ApiResponse
                {
                    Status = "Not Sent - Not a valid phone",
                    Reason = "Phone number too short"
                };
            }

            var currentHour = DateTime.UtcNow.Hour + 10;
            if (currentHour < 8 || currentHour > 21)
            {
                return new ApiResponse
                {
                    Status = "Not Sent - Not valid by Time zone",
                    Reason = "Outside allowed sending hours (8 AM - 9 PM AEST)"
                };
            }

            var random = new Random();
            if (random.Next(100) < 95)
            {
                return new ApiResponse
                {
                    Status = "Successfully Sent",
                    Reason = null
                };
            }
            else
            {
                return new ApiResponse
                {
                    Status = "Failed - API Error",
                    Reason = "Temporary API error"
                };
            }
        }

        private async Task UpdateMessageStatus(long messageId, string status, string reason)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                string query = @"
                    UPDATE [dbo].[Messages]
                    SET [Status] = @Status,
                        [StatusReason] = @Reason,
                        [ProcessedDateTime] = CASE WHEN @Status NOT IN ('Processing', 'Queued') 
                            THEN GETUTCDATE() ELSE [ProcessedDateTime] END,
                        [ModifiedDateTime] = GETUTCDATE()
                    WHERE [ID] = @MessageId";

                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@Status", status);
                    command.Parameters.AddWithValue("@Reason", (object)reason ?? DBNull.Value);
                    command.Parameters.AddWithValue("@MessageId", messageId);
                    await command.ExecuteNonQueryAsync();
                }
            }
        }
    }

    public class ApiResponse
    {
        public string Status { get; set; }
        public string Reason { get; set; }
    }

    public class ThirdPartyApiResponse
    {
        public string Status { get; set; }
        public string Reason { get; set; }
    }
}