using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Data.SqlClient;
using Azure.Storage.Queues;

namespace SmsMessageMaintenanceFunctions
{
    public class MessageReaderFunction
    {
        private readonly ILogger<MessageReaderFunction> _logger;
        private readonly QueueClient _queueClient;
        private readonly string _connectionString;

        public MessageReaderFunction(
            ILogger<MessageReaderFunction> logger,
            QueueClient queueClient)
        {
            _logger = logger;
            _queueClient = queueClient;
            _connectionString = Environment.GetEnvironmentVariable("SqlConnectionString");
        }

        [Function("MessageReader")]
        public async Task Run([TimerTrigger("0 */1 * * * *")] TimerInfo timerInfo)
        {
            _logger.LogInformation($"MessageReader function executed at: {DateTime.UtcNow}");
            
            try
            {
                // Get pending messages from database
                var pendingMessages = await GetPendingMessages();
                
                if (pendingMessages.Count == 0)
                {
                    _logger.LogInformation("No pending messages to process");
                    return;
                }
                
                _logger.LogInformation($"Found {pendingMessages.Count} pending messages");
                
                // Enqueue messages for processing
                int enqueuedCount = 0;
                foreach (var message in pendingMessages)
                {
                    try
                    {
                        await EnqueueMessage(message);
                        await UpdateMessageStatusToQueued(message.MessageId);
                        enqueuedCount++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Failed to enqueue message {message.MessageId}: {ex.Message}");
                    }
                }
                
                _logger.LogInformation($"Successfully enqueued {enqueuedCount} messages");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in MessageReader: {ex.Message}");
                throw;
            }
        }

        private async Task<List<QueueMessageDto>> GetPendingMessages()
        {
            var messages = new List<QueueMessageDto>();
            
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                
                string query = @"
                    SELECT TOP 100
                        [ID],
                        [To],
                        [From],
                        [Message]
                    FROM [dbo].[Messages]
                    WHERE [Status] = 'Pending'
                    ORDER BY [CreatedDateTime] ASC";
                
                using (var command = new SqlCommand(query, connection))
                {
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            messages.Add(new QueueMessageDto
                            {
                                MessageId = reader.GetInt64(0),
                                To = reader.GetInt64(1),
                                From = reader.GetInt64(2),
                                Message = reader.GetString(3)
                            });
                        }
                    }
                }
            }
            
            return messages;
        }

        private async Task EnqueueMessage(QueueMessageDto message)
        {
            var messageJson = JsonSerializer.Serialize(message);
            var messageBytes = Encoding.UTF8.GetBytes(messageJson);
            var base64Message = Convert.ToBase64String(messageBytes);
            
            await _queueClient.SendMessageAsync(base64Message);
            
            _logger.LogInformation($"Enqueued message {message.MessageId}");
        }

        private async Task UpdateMessageStatusToQueued(long messageId)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                
                string query = @"
                    UPDATE [dbo].[Messages]
                    SET [Status] = 'Queued',
                        [QueuedDateTime] = GETUTCDATE(),
                        [ModifiedDateTime] = GETUTCDATE()
                    WHERE [ID] = @MessageId";
                
                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@MessageId", messageId);
                    await command.ExecuteNonQueryAsync();
                }
            }
        }
    }

    public class TimerInfo
    {
        public TimerScheduleStatus ScheduleStatus { get; set; }
        public bool IsPastDue { get; set; }
    }

    public class TimerScheduleStatus
    {
        public DateTime Last { get; set; }
        public DateTime Next { get; set; }
        public DateTime LastUpdated { get; set; }
    }
}