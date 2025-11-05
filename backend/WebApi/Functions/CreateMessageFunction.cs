using System.Net;
using System.Text.Json;
using System.Web;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using SmsMessageMaintenanceModels;
using SmsMessageMaintenanceServices;

namespace SmsMessageMaintenanceFunctions
{
    /// <summary>
    /// Enhanced Azure Function for creating messages
    /// Supports multiple input formats: JSON, Query String, and Form-Data
    /// </summary>
    public class CreateMessageFunction
    {
        private readonly IMessageService _messageService;
        private readonly ILogger<CreateMessageFunction> _logger;

        public CreateMessageFunction(
            IMessageService messageService,
            ILogger<CreateMessageFunction> logger)
        {
            _messageService = messageService ?? throw new ArgumentNullException(nameof(messageService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [Function("CreateMessage")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "messages")] HttpRequestData req)
        {
            _logger.LogInformation("CreateMessage function processing request.");

            try
            {
                // Parse request body based on Content-Type
                MessageDto messageDto = await ParseRequestAsync(req);
                
                if (messageDto == null)
                {
                    return await CreateBadRequestResponse(req, "Invalid request format or missing required fields");
                }

                // Delegate business logic to service layer
                var response = await _messageService.CreateMessageAsync(messageDto);

                // Build successful HTTP response
                return await CreateSuccessResponse(req, response);
            }
            catch (ValidationException ex)
            {
                _logger.LogWarning($"Validation failed: {ex.Message}");
                return await CreateValidationErrorResponse(req, ex.Errors);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning($"Invalid argument: {ex.Message}");
                return await CreateBadRequestResponse(req, ex.Message);
            }
            catch (TimeoutException ex)
            {
                _logger.LogError($"Timeout: {ex.Message}");
                return await CreateErrorResponse(req, HttpStatusCode.RequestTimeout, 
                    "The request timed out. Please try again.");
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError($"Unauthorized: {ex.Message}");
                return await CreateErrorResponse(req, HttpStatusCode.ServiceUnavailable, 
                    "Service temporarily unavailable. Please contact support.");
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError($"Operation error: {ex.Message}");
                return await CreateErrorResponse(req, HttpStatusCode.ServiceUnavailable, 
                    ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Unexpected error in CreateMessage: {ex.Message}");
                return await CreateErrorResponse(req, HttpStatusCode.InternalServerError, 
                    "An unexpected error occurred while processing the request");
            }
        }

        /// <summary>
        /// Parses incoming request and determines format (JSON, Query String, or Form-Data)
        /// </summary>
        private async Task<MessageDto?> ParseRequestAsync(HttpRequestData req)
        {
            try
            {
                var contentType = req.Headers.TryGetValues("Content-Type", out var values) 
                    ? values.FirstOrDefault()?.ToLower() 
                    : string.Empty;

                _logger.LogInformation($"Processing request with Content-Type: {contentType}");

                // 1. Check for Query String parameters (can work with any method)
                if (req.Query.Count > 0)
                {
                    _logger.LogInformation("Parsing data from Query String");
                    return ParseFromQueryString(req.Query);
                }

                // 2. Check for JSON (application/json)
                if (contentType?.Contains("application/json") == true)
                {
                    _logger.LogInformation("Parsing data from JSON body");
                    return await ParseFromJson(req);
                }

                // 3. Check for Form-Data (application/x-www-form-urlencoded)
                if (contentType?.Contains("application/x-www-form-urlencoded") == true)
                {
                    _logger.LogInformation("Parsing data from Form-Data (URL encoded)");
                    return await ParseFromFormData(req);
                }

                // 4. Check for Multipart Form-Data (multipart/form-data)
                if (contentType?.Contains("multipart/form-data") == true)
                {
                    _logger.LogInformation("Parsing data from Multipart Form-Data");
                    return await ParseFromMultipartFormData(req);
                }

                _logger.LogWarning("Unsupported Content-Type or no data provided");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error parsing request: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Parse data from Query String parameters
        /// Example: ?to=1234567890&from=0987654321&message=Hello
        /// </summary>
        private MessageDto? ParseFromQueryString(
            System.Collections.Specialized.NameValueCollection query)
        {
            try
            {
                return new MessageDto
                {
                    To = query["to"],
                    From = query["from"],
                    Message = query["message"]
                };
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error parsing query string: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Parse data from JSON body
        /// Example: { "to": "1234567890", "from": "0987654321", "message": "Hello" }
        /// </summary>
        private async Task<MessageDto?> ParseFromJson(HttpRequestData req)
        {
            try
            {
                return await req.ReadFromJsonAsync<MessageDto>();
            }
            catch (JsonException ex)
            {
                _logger.LogError($"JSON parsing error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Parse data from Form-Data (application/x-www-form-urlencoded)
        /// Example: to=1234567890&from=0987654321&message=Hello
        /// </summary>
        private async Task<MessageDto?> ParseFromFormData(HttpRequestData req)
        {
            try
            {
                var body = await req.ReadAsStringAsync();
                if (string.IsNullOrEmpty(body))
                {
                    return null;
                }

                var formData = HttpUtility.ParseQueryString(body);
                
                return new MessageDto
                {
                    To = formData["to"],
                    From = formData["from"],
                    Message = formData["message"]
                };
            }
            catch (Exception ex)
            {
                _logger.LogError($"Form-data parsing error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Parse data from Multipart Form-Data
        /// Used when forms include file uploads or complex data
        /// </summary>
        private async Task<MessageDto?> ParseFromMultipartFormData(HttpRequestData req)
        {
            try
            {
                // For multipart/form-data, you'll need to parse the boundary and sections
                // This is a simplified version - for production, consider using a library
                var body = await req.ReadAsStringAsync();
                
                if (string.IsNullOrEmpty(body))
                {
                    return null;
                }

                var messageDto = new MessageDto();
                
                // Extract form fields from multipart data
                // This is a basic implementation - enhance for production use
                var fields = ParseMultipartFields(body);
                
                if (fields.ContainsKey("to"))
                    messageDto.To = fields["to"];
                if (fields.ContainsKey("from"))
                    messageDto.From = fields["from"];
                if (fields.ContainsKey("message"))
                    messageDto.Message = fields["message"];

                return messageDto;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Multipart form-data parsing error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Helper method to parse multipart form fields
        /// Basic implementation - enhance for production scenarios
        /// </summary>
        private Dictionary<string, string> ParseMultipartFields(string body)
        {
            var fields = new Dictionary<string, string>();
            
            // Split by boundary (simplified - production code should use proper multipart parser)
            var parts = body.Split(new[] { "\r\n\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var part in parts)
            {
                // Look for Content-Disposition header to extract field name
                if (part.Contains("Content-Disposition"))
                {
                    var lines = part.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
                    string fieldName = null;
                    string fieldValue = null;

                    foreach (var line in lines)
                    {
                        if (line.Contains("name=\""))
                        {
                            var start = line.IndexOf("name=\"") + 6;
                            var end = line.IndexOf("\"", start);
                            fieldName = line.Substring(start, end - start);
                        }
                        else if (!string.IsNullOrWhiteSpace(line) && 
                                 !line.Contains("Content-") && 
                                 fieldName != null)
                        {
                            fieldValue = line.Trim();
                            break;
                        }
                    }

                    if (fieldName != null && fieldValue != null)
                    {
                        fields[fieldName] = fieldValue;
                    }
                }
            }

            return fields;
        }

        private async Task<HttpResponseData> CreateSuccessResponse(
            HttpRequestData req, 
            MessageResponse messageResponse)
        {
            var httpResponse = req.CreateResponse(HttpStatusCode.Created);
            httpResponse.Headers.Add("Location", $"/api/messages/{messageResponse.Id}");
            await httpResponse.WriteAsJsonAsync(messageResponse);
            return httpResponse;
        }

        private async Task<HttpResponseData> CreateBadRequestResponse(
            HttpRequestData req, 
            string errorMessage)
        {
            var response = req.CreateResponse(HttpStatusCode.BadRequest);
            await response.WriteAsJsonAsync(new { error = errorMessage });
            return response;
        }

        private async Task<HttpResponseData> CreateValidationErrorResponse(
            HttpRequestData req, 
            System.Collections.Generic.List<string> errors)
        {
            var response = req.CreateResponse(HttpStatusCode.BadRequest);
            await response.WriteAsJsonAsync(new { errors });
            return response;
        }

        private async Task<HttpResponseData> CreateErrorResponse(
            HttpRequestData req, 
            HttpStatusCode statusCode, 
            string errorMessage)
        {
            var response = req.CreateResponse(statusCode);
            await response.WriteAsJsonAsync(new { error = errorMessage });
            return response;
        }
    }
}