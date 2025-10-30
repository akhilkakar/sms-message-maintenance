using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Data.SqlClient;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SmsMessageMaintenanceFunctions
{
    public class GetMessagesFunction
    {
        private readonly ILogger<GetMessagesFunction> _logger;
        private readonly string _connectionString;

        public GetMessagesFunction(ILogger<GetMessagesFunction> logger)
        {
            _logger = logger;
            _connectionString = Environment.GetEnvironmentVariable("SqlConnectionString") ?? throw new InvalidOperationException("SqlConnectionString environment variable is not set");
        }

        [Function("GetMessages")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "messages")] HttpRequestData req)
        {
            _logger.LogInformation("GetMessages function processing request.");

            try
            {
                // Parse query parameters
                var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
                string search = query["search"] ?? "";
                int page = int.TryParse(query["page"], out int p) && p > 0 ? p : 1;
                int pageSize = int.TryParse(query["pageSize"], out int ps) && ps > 0 && ps <= 100 ? ps : 10;
                string sortBy = query["sortBy"] ?? "createdDateTime";
                string sortOrder = query["sortOrder"]?.ToLower() == "asc" ? "ASC" : "DESC";

                // Validate sortBy to prevent SQL injection
                var validSortColumns = new[] { "id", "message", "status", "createdDateTime", "modifiedDateTime" };
                if (!validSortColumns.Contains(sortBy.ToLower()))
                {
                    sortBy = "createdDateTime";
                }

                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    // Get total count
                    int totalCount = await GetTotalCount(connection, search);

                    // Get paginated results
                    var messages = await GetMessages(connection, search, page, pageSize, sortBy, sortOrder);

                    var response = new
                    {
                        data = messages,
                        totalCount = totalCount,
                        page = page,
                        pageSize = pageSize,
                        totalPages = (int)Math.Ceiling((double)totalCount / pageSize)
                    };

                    var httpResponse = req.CreateResponse(HttpStatusCode.OK);
                    await httpResponse.WriteAsJsonAsync(response);
                    return httpResponse;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in GetMessages: {ex.Message}");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteAsJsonAsync(new { error = "An error occurred while processing the request" });
                return errorResponse;
            }
        }

        private async Task<int> GetTotalCount(SqlConnection connection, string search)
        {
            string countQuery = @"
                SELECT COUNT(*) 
                FROM [dbo].[Messages]
                WHERE (@Search = '' OR 
                       [Message] LIKE '%' + @Search + '%' OR 
                       [Status] LIKE '%' + @Search + '%' OR
                       CAST([To] AS NVARCHAR) LIKE '%' + @Search + '%' OR
                       CAST([From] AS NVARCHAR) LIKE '%' + @Search + '%')";

            using (var command = new SqlCommand(countQuery, connection))
            {
                command.Parameters.AddWithValue("@Search", search);
                return (int)(await command.ExecuteScalarAsync() ?? 0);
            }
        }

        private async Task<List<Message>> GetMessages(
            SqlConnection connection, 
            string search, 
            int page, 
            int pageSize, 
            string sortBy, 
            string sortOrder)
        {
            string query = $@"
                SELECT [ID], [To], [From], [Message], [Status], [StatusReason], 
                       [CreatedDateTime], [ModifiedDateTime], [QueuedDateTime], [ProcessedDateTime]
                FROM [dbo].[Messages]
                WHERE (@Search = '' OR 
                       [Message] LIKE '%' + @Search + '%' OR 
                       [Status] LIKE '%' + @Search + '%' OR
                       CAST([To] AS NVARCHAR) LIKE '%' + @Search + '%' OR
                       CAST([From] AS NVARCHAR) LIKE '%' + @Search + '%')
                ORDER BY [{sortBy}] {sortOrder}
                OFFSET @Offset ROWS
                FETCH NEXT @PageSize ROWS ONLY";

            var messages = new List<Message>();

            using (var command = new SqlCommand(query, connection))
            {
                command.Parameters.AddWithValue("@Search", search);
                command.Parameters.AddWithValue("@Offset", (page - 1) * pageSize);
                command.Parameters.AddWithValue("@PageSize", pageSize);

                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        messages.Add(new Message
                        {
                            Id = reader.GetInt64(0),
                            To = reader.GetInt64(1).ToString(),
                            From = reader.GetInt64(2).ToString(),
                            MessageText = reader.GetString(3),
                            Status = reader.IsDBNull(4) ? "Pending" : reader.GetString(4),
                            StatusReason = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                            CreatedDateTime = reader.GetDateTime(6),
                            ModifiedDateTime = reader.GetDateTime(7),
                            QueuedDateTime = reader.IsDBNull(8) ? (DateTime?)null : reader.GetDateTime(8),
                            ProcessedDateTime = reader.IsDBNull(9) ? (DateTime?)null : reader.GetDateTime(9)
                        });
                    }
                }
            }

            return messages;
        }
    }

    public class Message
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("to")]
        public required string To { get; set; }

        [JsonPropertyName("from")]
        public required string From { get; set; }

        [JsonPropertyName("message")]
        public required string MessageText { get; set; }

        [JsonPropertyName("status")]
        public required string Status { get; set; }

        [JsonPropertyName("statusReason")]
        public required string StatusReason { get; set; }

        [JsonPropertyName("createdDateTime")]
        public DateTime CreatedDateTime { get; set; }

        [JsonPropertyName("modifiedDateTime")]
        public DateTime ModifiedDateTime { get; set; }

        [JsonPropertyName("queuedDateTime")]
        public DateTime? QueuedDateTime { get; set; }

        [JsonPropertyName("processedDateTime")]
        public DateTime? ProcessedDateTime { get; set; }
    }
}